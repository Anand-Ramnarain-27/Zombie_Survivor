using UnityEngine;

namespace VolumetricLightsDemo
{
	public class CubeGen : MonoBehaviour
	{
		public int count;

		public float delay = 0.1f;

		private float last;

		private void Update()
		{
			if (!(Time.time - last < delay))
			{
				last = Time.time;
				GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
				obj.transform.position = base.transform.position;
				obj.transform.localScale = Vector3.one * Random.Range(0.5f, 1.5f);
				obj.transform.forward = Random.onUnitSphere;
				obj.AddComponent<Rigidbody>();
				if (--count < 0)
				{
					Object.Destroy(this);
				}
			}
		}
	}
}
