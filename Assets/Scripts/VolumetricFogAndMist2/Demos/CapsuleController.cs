using UnityEngine;

namespace VolumetricFogAndMist2.Demos
{
	public class CapsuleController : MonoBehaviour
	{
		public VolumetricFog fogVolume;

		public float moveSpeed = 10f;

		public float fogHoleRadius = 8f;

		public float clearDuration = 0.2f;

		public float distanceCheck = 1f;

		private Vector3 lastPos = new Vector3(float.MaxValue, 0f, 0f);

		private void Update()
		{
			float num = Time.deltaTime * moveSpeed;
			if (Input.GetKey(KeyCode.LeftArrow))
			{
				base.transform.Translate(0f - num, 0f, 0f);
			}
			else if (Input.GetKey(KeyCode.RightArrow))
			{
				base.transform.Translate(num, 0f, 0f);
			}
			if (Input.GetKey(KeyCode.UpArrow))
			{
				base.transform.Translate(0f, 0f, num);
			}
			else if (Input.GetKey(KeyCode.DownArrow))
			{
				base.transform.Translate(0f, 0f, 0f - num);
			}
			if ((base.transform.position - lastPos).magnitude > distanceCheck)
			{
				lastPos = base.transform.position;
				fogVolume.SetFogOfWarAlpha(base.transform.position, fogHoleRadius, 0f, clearDuration);
			}
		}
	}
}
