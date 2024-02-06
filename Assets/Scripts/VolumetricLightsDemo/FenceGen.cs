using UnityEngine;

namespace VolumetricLightsDemo
{
	public class FenceGen : MonoBehaviour
	{
		public int count;

		public float delay = 0.1f;

		public Vector3 step = new Vector3(0f, 0f, -2f);

		private float last;

		private Vector3 pos;

		private void Start()
		{
			pos = base.transform.position;
		}

		private void Update()
		{
			if (!(Time.time - last < delay))
			{
				last = Time.time;
				GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
				obj.transform.position = pos;
				pos += step;
				obj.transform.localScale = new Vector3(1f, 4f, 1f);
				if (--count < 0)
				{
					Object.Destroy(this);
				}
			}
		}
	}
}
