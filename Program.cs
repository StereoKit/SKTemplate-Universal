using StereoKit;
using StereoKit.Framework;
using StereoKitApp;
using System.Threading.Tasks;

public class Program
{
	public SKSettings Settings => new SKSettings {
		appName = "Light Baking",
		mode    = AppMode.XR,
		origin  = OriginMode.Floor
	};
	
	StaticScene scene;
	BakedScene  bakedScene;
	GridBuilder builder;
	bool        mapDirty = true;

	const int mapWidth  = 10;
	const int mapHeight = 10;
	byte[]    map = new byte[mapWidth*mapHeight] {
1,1,1,1,1,1,1,1,1,1,
1,0,0,0,0,0,0,0,0,1,
1,0,0,0,0,0,0,0,0,1,
1,0,1,1,0,0,1,1,0,1,
1,0,1,0,0,0,0,1,0,1,
1,0,1,0,0,0,0,1,0,1,
1,0,1,0,0,1,0,1,0,1,
1,0,1,1,0,1,0,1,0,1,
1,0,0,1,0,0,0,0,0,1,
1,1,0,1,1,1,1,1,0,1 };
	Color[] mapLights = new Color[mapWidth * mapHeight];

	float samples = 64;

	public static void Main(string[] args)
	{
		Program app = new Program();
		SK.Initialize(app.Settings);
		app.Init();
		SK.Run(app.Step);
	}

	public void Init()
	{
		builder = new GridBuilder(1,
			new TileDefinition(TileType.Full,      0,    Model.FromFile("TileEmpty.gltf")),
			new TileDefinition(TileType.Full,      1,    Model.FromFile("TileFull.glb")),
			new TileDefinition(TileType.Corner,    1, 0, Model.FromFile("TileCorner.gltf")),
			new TileDefinition(TileType.Edge,      1, 0, Model.FromFile("TileEdge.gltf")),
			new TileDefinition(TileType.InvCorner, 1, 0, Model.FromFile("TileInvCorner.gltf")),
			new TileDefinition(TileType.Kitty,     1, 0, Model.FromFile("TileKitty.gltf")) );
		scene = builder.MakeGrid(map,mapWidth,mapHeight);
		scene.AddModel(Model.FromFile("Lampshade.glb"), Matrix.TRS(new Vec3(-0.75f, 1.5f, -3.52f), new Vec3(-45,0,0), 0.5f));
		
		bakedScene = new BakedScene();
		bakedScene.SetSky(Renderer.SkyLight);
		mapLights[4 + 2 * mapWidth] = new Color(1, 1, 1);
		//bakedScene.AddLight(new Vec3(-0.75f, 1.5f, -3.52f), Color.HSV((float)r.NextDouble(), 0.4f+(float)r.NextDouble() * 0.5f, 1).ToLinear(), 200f);
	}

	public void Step()
	{
		bakedScene.Draw();
		bakedScene.DrawDebug();

		WindowMapEditor();

		if (mapDirty && !bakedScene.Baking)
		{
			mapDirty = false;
			RebuildMap(true);
		}
	}

	Pose  mapWindowPose = new Pose(0.2f, 1.5f, -0.5f, Quat.LookDir(0, 0, 1));
	int   mapEditMode = 0;
	float mapColorHue = 0;
	void WindowMapEditor()
	{
		UI.WindowBegin("Map Editor", ref mapWindowPose);

		if (UI.Radio("Map", mapEditMode == 0)) mapEditMode = 0;
		UI.SameLine();
		if (UI.Radio("Lighting", mapEditMode == 1)) mapEditMode = 1;

		if (mapEditMode == 1)
		{
			UI.SameLine();
			UI.Label("Hue");
			UI.SameLine();
			UI.PushTint(Color.HSV(mapColorHue, 0.2f, 1));
			UI.HSlider("Hue", ref mapColorHue, 0, 1, 0);
			UI.PopTint();
		}

		if (bakedScene.Baking)
		{
			UI.Label("Baking...");
			UI.SameLine();
			UI.ProgressBar(bakedScene.BakingProgress);
		} else {
			if (UI.Button("Bake"))
				RebuildMap(false);
			UI.SameLine();
			UI.Label("Samples"); UI.SameLine(); UI.HSlider("Samples", ref samples, 0, 512, 1);
		}

		UI.HSeparator();

		Vec2 btnSize = V.XY(0.03f, 0.03f);
		if (mapEditMode == 0)
		{
			for (int y = 0; y < mapHeight; y++)
			{
				for (int x = 0; x < mapWidth; x++)
				{
					int i = x + y * mapWidth;
					UI.PushId(i);
					bool solid = map[i] == 1;
					if (UI.Toggle(solid ? "X" : " ", ref solid, null, Sprite.ToggleOff, UIBtnLayout.CenterNoText, btnSize))
					{
						map[i] = solid ? (byte)1 : (byte)0;
						mapDirty = true;
					}
					UI.PopId();
					UI.SameLine();
				}
				UI.NextLine();
			}
		}
		else if (mapEditMode == 1)
		{
			for (int y = 0; y < mapHeight; y++)
			{
				for (int x = 0; x < mapWidth; x++)
				{
					int i = x + y * mapWidth;
					UI.PushId(i + 1000);
					bool present = mapLights[i].a > 0;
					UI.PushTint(present ? mapLights[i] : Color.White);
					if (UI.Toggle(present ? "*" : " ", ref present, null, Sprite.RadioOn, UIBtnLayout.CenterNoText, btnSize))
					{
						mapLights[i] = Color.HSV(mapColorHue, lightSat, lightVal).ToLinear();
						mapLights[i].a = present ? 1 : 0;
						mapDirty = true;
					}
					UI.PopTint();
					UI.PopId();
					UI.SameLine();
				}
				UI.NextLine();
			}
		}
		
		UI.WindowEnd();
	}

	const float lightSat = 0.3f;
	const float lightVal = 1;
	const float lightintensity = 0.9f;
	void RebuildMap(bool fast)
	{
		bakedScene.ClearLights();
		for (int y = 0; y < mapHeight; y++)
		{
			for (int x = 0; x < mapWidth; x++)
			{
				int i = x + y * mapWidth;
				if (mapLights[i].a > 0)
					bakedScene.AddPointLight(builder.TilePos(x, y, mapWidth, mapHeight) + new Vec3(-0.5f,1.5f,-0.5f), mapLights[i], lightintensity);
			}
		}

		bakedScene.AddDirectionalLight(new Vec3(-1, -1.1f, 0.4f), Color.HSV(0.1f, 0.3f, 1.0f).ToLinear(), .5f);
		scene = builder.MakeGrid(map, mapWidth, mapHeight);
		if (!bakedScene.Baking)
			Task.Run(() => bakedScene.Bake(scene, fast?0:(int)samples, !fast));
	}
}