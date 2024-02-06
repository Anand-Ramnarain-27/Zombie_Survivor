using System.Collections.Generic;
using UnityEngine;

namespace VolumetricFogAndMist2
{
	public class VolumetricFogSubVolume : MonoBehaviour
	{
		public VolumetricFogProfile profile;

		public float fadeDistance = 1f;

		public static List<VolumetricFogSubVolume> subVolumes = new List<VolumetricFogSubVolume>();

		private void OnDrawGizmos()
		{
			Gizmos.color = Color.white;
			Gizmos.DrawWireCube(base.transform.position, base.transform.localScale);
		}

		private void OnEnable()
		{
			if (!subVolumes.Contains(this))
			{
				subVolumes.Add(this);
			}
		}

		private void OnDisable()
		{
			if (subVolumes.Contains(this))
			{
				subVolumes.Remove(this);
			}
		}
	}
}
