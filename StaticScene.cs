using System.Collections.Generic;

namespace StereoKit.Framework
{
	public class StaticScene
	{
		internal List<StaticSceneItem> _items = new List<StaticSceneItem>();

		public void AddModel(Model model, Matrix at)
		{
			if (model == null) return;

			foreach (ModelNode node in model.Nodes)
			{
				if (node.Mesh == null) continue;
				
				string name = node.Name;
				StaticSceneItem item = new StaticSceneItem();
				item.visible      = name.Contains("[invisible]" ) == false;
				item.solid        = name.Contains("[intangible]") == false;
				item.material     = node.Material;
				item.mesh         = node.Mesh;
				item.transform    = node.ModelTransform * at;
				item.invTransform = item.transform.Inverse;
				_items.Add(item);
			}
		}

		public bool Raycast(Ray worldRay, out Ray at)
		{
			float closest = float.MaxValue;
			at = default;
			for (int i = 0; i < _items.Count; i++)
			{
				if (_items[i].Raycast(worldRay, out Ray curr))
				{
					float dist = Vec3.DistanceSq(curr.position, worldRay.position);
					if (dist < closest)
					{
						closest = dist;
						at      = curr;
					}
				}
			}
			return closest != float.MaxValue;
		}

		public void Draw()
		{
			for (int i = 0; i < _items.Count; i++)
				_items[i].Draw();
		}
	}

	struct StaticSceneItem
	{
		internal Matrix   transform;
		internal Matrix   invTransform;
		internal Mesh     mesh;
		internal Material material;
		internal bool     solid;
		internal bool     visible;

		public bool Raycast(Ray worldRay, out Ray intersection)
		{
			if (solid)
			{
				bool result = mesh.Intersect(invTransform.Transform(worldRay), out intersection);
				if (result) intersection = transform.Transform(intersection);
				return result;
			}
			else
			{
				intersection = default;
				return false;
			}
		}

		public void Draw() { if (visible) mesh.Draw(material, transform); }
	}
}
