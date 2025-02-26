using StereoKit;
using StereoKit.Framework;

namespace StereoKitApp
{
	enum TileType
	{
		/// <summary> Corner tile:
		/// 1 0
		/// 0 0
		/// </summary>
		Corner,
		/// <summary> Half edge tile:
		/// 1 0
		/// 1 0
		/// </summary>
		Edge,
		/// <summary> Kitty corner tile:
		/// 1 0
		/// 0 1
		/// </summary>
		Kitty,
		/// <summary> The inverted corner tile:
		/// 1 1
		/// 1 0
		/// </summary>
		InvCorner,
		/// <summary> Completely full tile:
		/// 1 1
		/// 1 1
		/// </summary>
		Full,
		Max,
	}

	struct TileDefinition
	{
		public Model    model;
		public TileType type;
		public int      matchTile1;
		public int      matchTile0;

		public TileDefinition(TileType type, int matchTile1, int matchTile0, Model model) { this.type = type; this.matchTile1 = matchTile1; this.matchTile0 = matchTile0; this.model = model; }
		public TileDefinition(TileType type, int matchTile1, Model model) { this.type = type; this.matchTile1 = matchTile1; this.matchTile0 = -1; this.model = model; }
	}
	
	class GridBuilder
	{
		TileDefinition[] tiles;
		float            tileSize;

		public GridBuilder(float tileSize, params TileDefinition[] tiles)
		{
			this.tileSize = tileSize;
			this.tiles    = tiles;
		}

		public StaticScene MakeGrid(string grid)
		{
			string[] lines = grid.Split('\n');
			int      w     = lines[0].Trim().Length;
			int      h     = lines.Length;
			byte[]   map   = new byte[w * h];
			for (int y = 0; y < h; y++)
			{
				for (int x = 0; x < w; x++)
					map[x+y*w] = (byte)(lines[y][x] - '0');
			}
			return MakeGrid(map, w, h);
		}

		public StaticScene MakeGrid(byte[] grid, int w, int h)
		{
			StaticScene result = new StaticScene();
			for (int y = 0; y < h-1; y++)
			{
				for (int x = 0; x < w-1; x++)
				{
					int a = grid[ x   + y   *w];
					int b = grid[(x+1)+ y   *w];
					int c = grid[x    +(y+1)*w];
					int d = grid[(x+1)+(y+1)*w];

					if (!TileId(a,b,c,d, out Model model, out float rot)) return null;

					result.AddModel(model, Matrix.TR(
						TilePos(x, y, w, h),
						Quat.FromAngles(0, rot, 0)));
				}
			}
			return result;
		}

		public Vec3 TilePos(int x, int y, int width, int height)
			=> new Vec3(
				(x - ((width - 1) / 2.0f)) * tileSize,
				0,
				(y - ((height - 1) / 2.0f)) * tileSize);

		bool TileId(int a, int b, int c, int d, out Model model, out float rotationDegrees)
		{
			model           = null;
			rotationDegrees = 0;

			// Figure out what the two tile types are
			int match1 = a;
			int match0 = a;
			if      (b > match1) match1 = b;
			else if (c > match1) match1 = c;
			else if (d > match1) match1 = d;
			if      (b < match0) match0 = b;
			else if (c < match0) match0 = c;
			else if (d < match0) match0 = d;

			// Find the type and rotation of this tile
			TileType type = TileType.Full;
			int      id   =
				(a==match1?1:0)<<3 |
				(b==match1?1:0)<<2 |
				(c==match1?1:0)<<1 |
				(d==match1?1:0);
			switch (id)
			{
				case 0b0000: type = TileType.Full;      break;
				case 0b1111: type = TileType.Full;      break;
				case 0b1000: type = TileType.Corner;    rotationDegrees = 180;break;
				case 0b0100: type = TileType.Corner;    rotationDegrees = 90; break;
				case 0b0001: type = TileType.Corner;    rotationDegrees = 0;  break;
				case 0b0010: type = TileType.Corner;    rotationDegrees = 270;break;
				case 0b1010: type = TileType.Edge;      rotationDegrees = 180;break;
				case 0b1100: type = TileType.Edge;      rotationDegrees = 90; break;
				case 0b0101: type = TileType.Edge;      rotationDegrees = 0;  break;
				case 0b0011: type = TileType.Edge;      rotationDegrees = 270;break;
				case 0b1001: type = TileType.Kitty;     break;
				case 0b0110: type = TileType.Kitty;     rotationDegrees = 90;break;
				case 0b1110: type = TileType.InvCorner; rotationDegrees = 180;break;
				case 0b1101: type = TileType.InvCorner; rotationDegrees = 90; break;
				case 0b0111: type = TileType.InvCorner; rotationDegrees = 0;  break;
				case 0b1011: type = TileType.InvCorner; rotationDegrees = 270; break;
				default: return false;
			}

			// Find a definition that matches the tile type and ids
			int gridDef = -1;
			for (int i = 0; i < tiles.Length; i++)
			{
				if (tiles[i].type == type                &&
					 match1       == tiles[i].matchTile1 &&
					(match0       == tiles[i].matchTile0 || tiles[i].matchTile0 == -1))
				{
					gridDef = i;
					break;
				}
			}
			if (gridDef < 0) return false;

			model = tiles[gridDef].model;

			return true;
		}
	}
}
