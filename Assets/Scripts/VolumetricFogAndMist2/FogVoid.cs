using UnityEngine;

namespace VolumetricFogAndMist2
{
	[ExecuteInEditMode]
	public class FogVoid : MonoBehaviour
	{
		[Range(0f, 1f)]
		public float roundness = 0.5f;

		[Range(0f, 1f)]
		public float falloff = 0.5f;

		private void OnEnable()
		{
			VolumetricFogManager.fogVoidManager.RegisterFogVoid(this);
		}

		private void OnDisable()
		{
			if (VolumetricFogManager.GetManagerIfExists() != null)
			{
				VolumetricFogManager.fogVoidManager.UnregisterFogVoid(this);
			}
		}

		private void OnDrawGizmosSelected()
		{
			Gizmos.color = new Color(1f, 1f, 0f, 0.75f);
			if (VolumetricFogManager.allowFogVoidRotation)
			{
				Gizmos.matrix = base.transform.localToWorldMatrix;
				Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
			}
			else
			{
				Gizmos.DrawWireCube(base.transform.position, base.transform.lossyScale);
			}
		}
	}
}
