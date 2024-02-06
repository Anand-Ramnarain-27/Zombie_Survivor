using System.Collections.Generic;
using UnityEngine;

namespace VolumetricFogAndMist2
{
	[ExecuteInEditMode]
	[DefaultExecutionOrder(100)]
	public class FogVoidManager : MonoBehaviour, IVolumetricFogManager
	{
		public static bool usingVoids;

		public const int MAX_FOG_VOID = 8;

		[Header("Void Search Settings")]
		public Transform trackingCenter;

		[Tooltip("Fog voids are sorted by camera distance every certain time interval to ensure the nearest 8 voids are used.")]
		public float distanceSortTimeInterval = 3f;

		private readonly List<FogVoid> fogVoids = new List<FogVoid>();

		private Vector4[] fogVoidPositions;

		private Vector4[] fogVoidSizes;

		private Matrix4x4[] fogVoidMatrices;

		private float distanceSortLastTime;

		private bool requireRefresh;

		public string managerName => "Fog Void Manager";

		private void OnEnable()
		{
			if (trackingCenter == null)
			{
				Camera cam = null;
				Tools.CheckCamera(ref cam);
				if (cam != null)
				{
					trackingCenter = cam.transform;
				}
			}
			if (fogVoidPositions == null || fogVoidPositions.Length != 8)
			{
				fogVoidPositions = new Vector4[8];
			}
			if (fogVoidSizes == null || fogVoidSizes.Length != 8)
			{
				fogVoidSizes = new Vector4[8];
			}
			if (fogVoidMatrices == null || fogVoidMatrices.Length != 8)
			{
				fogVoidMatrices = new Matrix4x4[8];
			}
		}

		private void LateUpdate()
		{
			if (!usingVoids)
			{
				return;
			}
			usingVoids = false;
			if (requireRefresh)
			{
				requireRefresh = false;
				TrackFogVoids(forceImmediateUpdate: true);
			}
			else
			{
				if (fogVoids.Count == 0)
				{
					return;
				}
				TrackFogVoids();
			}
			SubmitFogVoidData();
		}

		private void SubmitFogVoidData()
		{
			bool allowFogVoidRotation = VolumetricFogManager.allowFogVoidRotation;
			int num = 0;
			int count = fogVoids.Count;
			int num2 = 0;
			while (num < 8 && num2 < count)
			{
				FogVoid fogVoid = fogVoids[num2];
				if (!(fogVoid == null) && fogVoid.isActiveAndEnabled)
				{
					Transform transform = fogVoid.transform;
					Vector3 position = transform.position;
					Vector3 lossyScale = transform.lossyScale;
					if (lossyScale.x < 0.01f || lossyScale.z < 0.01f)
					{
						lossyScale.x = Mathf.Max(lossyScale.x, 0.01f);
						lossyScale.y = Mathf.Max(lossyScale.y, 0.01f);
						lossyScale.z = Mathf.Max(lossyScale.z, 0.01f);
					}
					lossyScale.x *= 0.5f;
					lossyScale.y *= 0.5f;
					lossyScale.z *= 0.5f;
					fogVoidPositions[num].x = position.x;
					fogVoidPositions[num].y = position.y;
					fogVoidPositions[num].z = position.z;
					fogVoidPositions[num].w = 10f * (1f - fogVoid.falloff) * (1f - fogVoid.falloff);
					if (allowFogVoidRotation)
					{
						fogVoidMatrices[num] = Matrix4x4.TRS(position, transform.rotation, lossyScale).inverse;
					}
					else
					{
						float x = lossyScale.x;
						float y = lossyScale.y;
						float z = lossyScale.z;
						fogVoidSizes[num].x = 1f / (0.0001f + x * x);
						fogVoidSizes[num].y = 1f / (0.0001f + y * y);
						fogVoidSizes[num].z = 1f / (0.0001f + z * z);
					}
					fogVoidSizes[num].w = fogVoid.roundness;
					num++;
				}
				num2++;
			}
			Shader.SetGlobalVectorArray(ShaderParams.VoidPositions, fogVoidPositions);
			if (allowFogVoidRotation)
			{
				Shader.SetGlobalMatrixArray(ShaderParams.VoidMatrices, fogVoidMatrices);
			}
			Shader.SetGlobalVectorArray(ShaderParams.VoidSizes, fogVoidSizes);
			Shader.SetGlobalInt(ShaderParams.VoidCount, num);
		}

		public void RegisterFogVoid(FogVoid fogVoid)
		{
			if (fogVoid != null)
			{
				fogVoids.Add(fogVoid);
				requireRefresh = true;
			}
		}

		public void UnregisterFogVoid(FogVoid fogVoid)
		{
			if (fogVoid != null && fogVoids.Contains(fogVoid))
			{
				fogVoids.Remove(fogVoid);
				requireRefresh = true;
			}
		}

		public void TrackFogVoids(bool forceImmediateUpdate = false)
		{
			if (fogVoids != null && fogVoids.Count > 0 && (forceImmediateUpdate || !Application.isPlaying || (distanceSortTimeInterval > 0f && Time.time - distanceSortLastTime > distanceSortTimeInterval)) && trackingCenter != null)
			{
				distanceSortLastTime = Time.time;
				fogVoids.Sort(fogVoidDistanceComparer);
			}
		}

		private int fogVoidDistanceComparer(FogVoid v1, FogVoid v2)
		{
			float sqrMagnitude = (v1.transform.position - trackingCenter.position).sqrMagnitude;
			float sqrMagnitude2 = (v2.transform.position - trackingCenter.position).sqrMagnitude;
			if (sqrMagnitude < sqrMagnitude2)
			{
				return -1;
			}
			if (sqrMagnitude > sqrMagnitude2)
			{
				return 1;
			}
			return 0;
		}

		public void Refresh()
		{
			requireRefresh = true;
		}
	}
}
