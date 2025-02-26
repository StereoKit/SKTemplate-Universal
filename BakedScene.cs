using System.Collections.Generic;
using System;
using System.Threading.Tasks;

namespace StereoKit.Framework
{
	public class BakedScene
	{
		List<BakeLight>    _lights = new List<BakeLight>();
		BakedSceneItem[]   _items;
		StaticScene        _scene;
		SphericalHarmonics _ambient;
		SphericalHarmonics _sky;
		int                _bounceSamples;

		public bool  Baking         { get; private set; }
		public float BakingProgress { get; private set; }
		public float LightCutoff    { get; set; } = 1/255.0f;

		public BakedScene() { }

		public void AddPointLight      (Vec3 at,  Color color, float intensity) { _lights.Add(new BakeLight(at, color, intensity, false)); }
		public void AddDirectionalLight(Vec3 dir, Color color, float intensity) { _lights.Add(new BakeLight(-dir.Normalized, color, intensity, true)); }
		public void SetAmbient         (SphericalHarmonics ambient) => _ambient = ambient;
		public void SetSky             (SphericalHarmonics sky)     => _sky     = sky;

		public void ClearLights() => _lights.Clear();

		public void Bake(StaticScene scene, int bounceSamples = 0, bool areaLights = true)
		{
			_bounceSamples = bounceSamples;
			_scene = scene;
			Baking = true;
			BakingProgress = 0;

			long totalVerts = 0;
			long currVerts  = 0;

			Dictionary<Mesh, Vertex[]> vertexCache = new Dictionary<Mesh, Vertex[]>();
			Dictionary<Mesh, uint  []> indexCache  = new Dictionary<Mesh, uint  []>();
			Dictionary<string, Material> textureMaterials = new Dictionary<string, Material>(); 

			// Build our list of mesh/material pairs
			List<BakedSceneItem> itemList = new List<BakedSceneItem>();
			for (int i = 0; i < scene._items.Count; i++)
			{
				if (!scene._items[i].visible) continue;
				Tex tex = scene._items[i].material.GetTexture("diffuse");
				if (!textureMaterials.TryGetValue(tex.Id, out Material mat))
				{
					mat = Material.Unlit.Copy();
					mat[MatParamName.DiffuseTex] = tex;
					textureMaterials[tex.Id] = mat;
				}

				int item = itemList.FindIndex(t => t.material == mat);
				if (item == -1)
				{
					BakedSceneItem sceneItem = new BakedSceneItem();
					sceneItem.material = mat;
					itemList.Add(sceneItem);
				}

				// cache mesh data while we're at it
				if (!vertexCache.TryGetValue(scene._items[i].mesh, out Vertex[] meshVerts))
				{
					meshVerts = scene._items[i].mesh.GetVerts();
					vertexCache[scene._items[i].mesh] = meshVerts;
					indexCache [scene._items[i].mesh] = scene._items[i].mesh.GetInds();
				}
			}
			BakedSceneItem[] itemArray = itemList.ToArray();

			// allocate memory for the baked verts
			for (int i = 0; i < scene._items.Count; i++)
			{
				if (!scene._items[i].visible) continue;

				Tex      tex = scene._items[i].material.GetTexture("diffuse");
				Material mat = textureMaterials[tex.Id];

				int item = Array.FindIndex(itemArray,t => t.material == mat);
				itemArray[item].vertCount += scene._items[i].mesh.VertCount;
				itemArray[item].indCount  += scene._items[i].mesh.IndCount;
			}
			for (int i = 0; i <itemArray.Length; i++)
			{
				itemArray[i].verts = new Vertex[itemArray[i].vertCount];
				itemArray[i].inds  = new uint  [itemArray[i].indCount];

				totalVerts += itemArray[i].vertCount;
			}

			
			// Bake all the materials!
			for (int i = 0; i < scene._items.Count; i++)
			{
				if (!scene._items[i].visible) continue;

				Tex      tex = scene._items[i].material.GetTexture("diffuse");
				Material mat = textureMaterials[tex.Id];

				int      item      = Array.FindIndex(itemArray, t => t.material == mat);
				Vertex[] meshVerts = vertexCache[scene._items[i].mesh];
				
				BakeMesh(vertexCache[scene._items[i].mesh], itemArray[item].verts, itemArray[item].vertCurr, scene._items[i].transform, areaLights);

				uint[] meshInds = indexCache[scene._items[i].mesh];
				for (int t = 0; t < meshInds.Length; t++)
					itemArray[item].inds[itemArray[item].indCurr + t] = meshInds[t] + itemArray[item].vertCurr;

				itemArray[item].indCurr  += (uint)meshInds .Length;
				itemArray[item].vertCurr += (uint)meshVerts.Length;

				currVerts += meshVerts.Length;
				BakingProgress = (float)(currVerts / (double)totalVerts);
			}

			// And generate the meshes
			for (int i = 0; i < itemArray.Length; i++)
			{
				itemArray[i].mesh = new Mesh();
				itemArray[i].mesh.SetVerts(itemArray[i].verts);
				itemArray[i].mesh.SetInds (itemArray[i].inds);
				itemArray[i].verts = null;
				itemArray[i].inds  = null;
			}

			_items = itemArray;
			Baking = false;
			BakingProgress = 1;
		}

		private void BakeMesh(Vertex[] verts, Vertex[] to, uint toOffset, Matrix at, bool areaLights)
		{
			//for (int v = 0; v < verts.Length; v++)
			Parallel.For(0, verts.Length, (v) =>
			{
				Vertex vert = verts[v];
				vert.pos  = at.Transform(vert.pos);
				vert.norm = at.TransformNormal(vert.norm).Normalized;

				Color amb = _ambient.Sample(vert.norm);
				Color c   = SamplePoint(vert.pos, vert.norm, _bounceSamples, areaLights);
				c.r += amb.r;
				c.g += amb.g;
				c.b += amb.b;

				if (c.r > 1) c.r = 1;
				if (c.g > 1) c.g = 1;
				if (c.b > 1) c.b = 1;
				vert.col = c;

				to[v + toOffset] = vert;
			}
			);
		}

		/*Vec2[] lightSampleOffsets = new Vec2[] {
			new Vec2( 0, 0),
			new Vec2(-1, 1),
			new Vec2( 1, 1),
			new Vec2(-1,-1),
			new Vec2( 1,-1),
		};*/
		/*Vec2[] lightSampleOffsets = new Vec2[] {
			new Vec2( .25f, .97f),
			new Vec2( .71f,-.71f),
			new Vec2(-.97f,-.26f),
		};*/
		/*Vec2[] lightSampleOffsets = new Vec2[] {
			new Vec2( .34f, .94f),
			new Vec2( .64f,-.76f),
			new Vec2(-.98f,-.17f),
		};*/
		Vec2[] lightSampleOffsets = new Vec2[] {
			new Vec2(-.42f, .9f),
			new Vec2( .9f,  .42f),
			new Vec2( .42f,-.9f),
			new Vec2(-.9f, -.42f),
		};
		private float LightVisibility(Vec3 lightPos, Vec3 from, float checkSize, bool smoothed)
		{
			if (!smoothed)
			{
				Vec3 directDir = lightPos - from;
				return _scene.Raycast(new Ray(from, directDir), out Ray hit) && Vec3.DistanceSq(from, hit.position) < directDir.MagnitudeSq
					? 0
					: 1;
			}

			Vec3 dir   = (lightPos - from).Normalized;
			Vec3 right =  Vec3.PerpendicularRight(dir, Vec3.Up);
			Vec3 up    = -Vec3.PerpendicularRight(dir, right);

			float coverage = 0;
			int   count    = smoothed ? lightSampleOffsets.Length : 1;
			for (int i = 0; i < count; i++)
			{
				Vec3 currLight = lightPos +
					lightSampleOffsets[i].x * right * checkSize +
					lightSampleOffsets[i].y * up * checkSize;
				Vec3 currDir = currLight - from;

				float distSq = 100000000;
				if (_scene.Raycast(new Ray(from, currDir), out Ray hit))
				{
					distSq = Vec3.DistanceSq(from, hit.position);
				}

				if (distSq + 0.01f > currDir.MagnitudeSq)
					coverage += 1;
			}

			return (coverage / count);
		}

		bool debug = false;

		static Vec3 Quantize(Vec3 v, float by)
		{
			return new Vec3(
				(int)(v.x * by) / by,
				(int)(v.y * by) / by,
				(int)(v.z * by) / by);
		}

		private Color SamplePoint(Vec3 at, Vec3 norm, int bounceSamples, bool areaLights)
		{
			// Offset the position from the surface, just a bit
			at += norm * 0.01f;

			// Quantize the position to help remove small variations in
			// lighting from one vertex to the next
			at   = Quantize(at,   200);
			norm = Quantize(norm, 200);

			Noise.NextSeed = new Noise.Seed { seed = (uint)(at.x * 1017 + at.y * 37000 + at.z * 12789) };

			Color c = new Color(0, 0, 0, 1);
			for (int i = 0; i < _lights.Count; i++)
			{
				Color lc = _lights[i].color;
				float vis = 0;
				if (_lights[i].directional)
				{
					vis = LightVisibility(at + _lights[i].pos * 1000.0f, at, 100.0f, areaLights);
				}
				else
				{
					Vec3  dir       = _lights[i].pos - at;
					float magSq     = dir.MagnitudeSq;
					float intensity = (1.0f / magSq) * _lights[i].intensity * Math.Max(0, Vec3.Dot(dir/SKMath.Sqrt(magSq), norm));
					// Skip the light if we know it's not bright enough early!
					if (intensity > LightCutoff)
						vis = LightVisibility(_lights[i].pos, at, 0.3f, areaLights) * intensity;
				}
				c = new Color(c.r + lc.r * vis, c.g + lc.g * vis, c.b + lc.b * vis, c.a);
			}

			if (debug) Mesh.Sphere.Draw(Material.Unlit, Matrix.TS(at, 0.2f), c);

			Vec3  right = Vec3.PerpendicularRight(norm, new Vec3(0, 1, -1.003f)).Normalized;
			Vec3  up    = Vec3.PerpendicularRight(right, norm).Normalized;
			float mod   = (1.0f / bounceSamples);
			for (int i=0;i<bounceSamples;i+=1)
			{
				Vec3 sample  = SampleHemisphere_Uniform((uint)i, (uint)bounceSamples);
				Vec3 currDir = right*sample.x + norm*sample.y + up*sample.z;

				Color bounceColor = _scene.Raycast(new Ray(at, currDir), out Ray hit)
					? SamplePoint(hit.position, hit.direction, 0, false)
					: _sky.Sample(currDir);
				c.r += bounceColor.r * mod;
				c.g += bounceColor.g * mod;
				c.b += bounceColor.b * mod;
			}

			return c;
		}

		Vec2 Hammersley(uint i, uint numSamples)
		{
			uint b = i;

			b = (b << 16) | (b >> 16);
			b = ((b & 0x55555555) << 1) | ((b & 0xAAAAAAAA) >> 1);
			b = ((b & 0x33333333) << 2) | ((b & 0xCCCCCCCC) >> 2);
			b = ((b & 0x0F0F0F0F) << 4) | ((b & 0xF0F0F0F0) >> 4);
			b = ((b & 0x00FF00FF) << 8) | ((b & 0xFF00FF00) >> 8);

			float radicalInverseVDC = b * 2.3283064365386963e-10f;
			return new Vec2((i / (float)numSamples), radicalInverseVDC);
		}

		Vec3 SampleHemisphere_Uniform(uint i, uint numSamples)
		{
			// Returns a 3D sample vector orientated around (0.0, 1.0, 0.0)
			// For practical use, must rotate with a rotation matrix (or whatever
			// your preferred approach is) for use with normals, etc.

			Vec2 xi = Hammersley(i, numSamples);

			float phi      = xi.y * 2.0f * (float)Math.PI;
			float cosTheta = 1.0f - xi.x;
			float sinTheta = SKMath.Sqrt(1.0f - cosTheta * cosTheta);

			return new Vec3(SKMath.Cos(phi) * sinTheta, cosTheta, SKMath.Sin(phi) * sinTheta);
		}

		public bool Raycast(Ray worldRay, out Ray at)
			=> _scene.Raycast(worldRay, out at);
		
		public void Draw()
		{
			if (_items != null)
			{
				for (int i = 0; i < _items.Length; i++)
					_items[i].Draw();
			}
			else if (_scene != null)
			{
				_scene.Draw();
			}
		}

		public void DrawDebug()
		{
			for (int i = 0; i < _lights.Count; i++)
			{
				if (_lights[i].directional) continue;
				Mesh.Sphere.Draw(Material.Unlit, Matrix.TS(_lights[i].pos, 0.1f), _lights[i].color);
			}

			if (Input.Key(Key.Space).IsActive() && _scene.Raycast(Input.Pointer(0, InputSource.HandRight).ray, out Ray hit))
			{
				debug = true;
				SamplePoint(hit.position, hit.direction, _bounceSamples, true);
				debug = false;
			}
			if (Input.Key(Key.F).IsJustActive())
			{
				for (int i = 0; i < _items.Length; i++)
					_items[i].material.Wireframe = true;
			}
			if (Input.Key(Key.F).IsJustInactive())
			{
				for (int i = 0; i < _items.Length; i++)
					_items[i].material.Wireframe = false;
			}
		}
	}

	struct BakedSceneItem
	{
		internal Mesh     mesh;
		internal Material material;

		internal Vertex[] verts;
		internal uint  [] inds;
		internal int      vertCount;
		internal int      indCount;
		internal uint     vertCurr;
		internal uint     indCurr;

		public void Draw() => mesh.Draw(material, Matrix.Identity);
	}

	struct BakeLight
	{
		internal Vec3  pos;
		internal Color color;
		internal float intensity;
		internal bool  directional;

		public BakeLight(Vec3 pos, Color color, float intensity, bool directional) { this.pos = pos; this.color = color; this.intensity = intensity; this.directional = directional; }
	}
}
