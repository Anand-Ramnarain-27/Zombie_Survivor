using System.Collections.Generic;
using UnityEngine;

namespace VolumetricFogAndMist2
{
	[ExecuteInEditMode]
	[DefaultExecutionOrder(100)]
	public class PointLightManager : MonoBehaviour, IVolumetricFogManager
	{
		public static bool usingPointLights;

		public const int MAX_POINT_LIGHTS = 16;

		[Header("Point Light Search Settings")]
		[Tooltip("Point lights are sorted by distance to tracking center object")]
		public Transform trackingCenter;

		[Tooltip("Point lights are sorted by camera distance every certain time interval to ensure the nearest 16 point lights are used.")]
		public float distanceSortTimeInterval = 3f;

		[Tooltip("Ignore point lights behind camera")]
		public bool excludeLightsBehind = true;

		[Header("Common Settings")]
		[Tooltip("Global inscattering multiplier for point lights")]
		public float inscattering = 1f;

		[Tooltip("Global intensity multiplier for point lights")]
		public float intensity = 1f;

		[Tooltip("Reduces light intensity near point lights")]
		public float insideAtten;

		private readonly List<FogPointLight> pointLights = new List<FogPointLight>();

		private Vector4[] pointLightColorBuffer;

		private Vector4[] pointLightPositionBuffer;

		private float distanceSortLastTime;

		private bool requireRefresh;

		public string managerName => "Point Light Manager";

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
			if (pointLightColorBuffer == null || pointLightColorBuffer.Length != 16)
			{
				pointLightColorBuffer = new Vector4[16];
			}
			if (pointLightPositionBuffer == null || pointLightPositionBuffer.Length != 16)
			{
				pointLightPositionBuffer = new Vector4[16];
			}
		}

		private void LateUpdate()
		{
			if (!usingPointLights)
			{
				return;
			}
			usingPointLights = false;
			if (requireRefresh)
			{
				requireRefresh = false;
				TrackPointLights(forceImmediateUpdate: true);
			}
			else
			{
				if (pointLights.Count == 0)
				{
					return;
				}
				TrackPointLights();
			}
			SubmitPointLightData();
		}

		private void SubmitPointLightData()
		{
			int num = 0;
			bool isPlaying = Application.isPlaying;
			bool flag = excludeLightsBehind && isPlaying;
			Vector3 vector;
			Vector3 lhs;
			if (flag && trackingCenter != null)
			{
				vector = trackingCenter.position;
				lhs = trackingCenter.forward;
			}
			else
			{
				vector = (lhs = Vector3.zero);
				flag = false;
			}
			int count = pointLights.Count;
			for (int i = 0; num < 16 && i < count; i++)
			{
				FogPointLight fogPointLight = pointLights[i];
				if (fogPointLight == null || !fogPointLight.isActiveAndEnabled)
				{
					continue;
				}
				Light pointLight = pointLights[i].pointLight;
				if (pointLight == null || !pointLight.isActiveAndEnabled || pointLight.type != LightType.Point)
				{
					continue;
				}
				Vector3 position = pointLight.transform.position;
				float range = pointLight.range;
				range *= fogPointLight.inscattering * inscattering / 25f;
				if (flag)
				{
					Vector3 vector2 = position - vector;
					if (Vector3.Dot(lhs, position - vector) < 0f && vector2.sqrMagnitude > range * range)
					{
						continue;
					}
				}
				float num2 = pointLight.intensity * fogPointLight.intensity * intensity;
				if (range > 0f && num2 > 0f)
				{
					pointLightPositionBuffer[num].x = position.x;
					pointLightPositionBuffer[num].y = position.y;
					pointLightPositionBuffer[num].z = position.z;
					pointLightPositionBuffer[num].w = 0f;
					Color color = pointLight.color;
					pointLightColorBuffer[num].x = color.r * num2;
					pointLightColorBuffer[num].y = color.g * num2;
					pointLightColorBuffer[num].z = color.b * num2;
					pointLightColorBuffer[num].w = range;
					num++;
				}
			}
			Shader.SetGlobalVectorArray(ShaderParams.PointLightColors, pointLightColorBuffer);
			Shader.SetGlobalVectorArray(ShaderParams.PointLightPositions, pointLightPositionBuffer);
			Shader.SetGlobalFloat(ShaderParams.PointLightInsideAtten, insideAtten);
			Shader.SetGlobalInt(ShaderParams.PointLightCount, num);
		}

		public void RegisterPointLight(FogPointLight fogPointLight)
		{
			if (fogPointLight != null)
			{
				pointLights.Add(fogPointLight);
				requireRefresh = true;
			}
		}

		public void UnregisterPointLight(FogPointLight fogPointLight)
		{
			if (fogPointLight != null && pointLights.Contains(fogPointLight))
			{
				pointLights.Remove(fogPointLight);
				requireRefresh = true;
			}
		}

		public void TrackPointLights(bool forceImmediateUpdate = false)
		{
			if (pointLights != null && pointLights.Count > 0 && (forceImmediateUpdate || !Application.isPlaying || (distanceSortTimeInterval > 0f && Time.time - distanceSortLastTime > distanceSortTimeInterval)) && trackingCenter != null)
			{
				distanceSortLastTime = Time.time;
				pointLights.Sort(pointLightsDistanceComparer);
			}
		}

		private int pointLightsDistanceComparer(FogPointLight l1, FogPointLight l2)
		{
			float sqrMagnitude = (l1.transform.position - trackingCenter.position).sqrMagnitude;
			float sqrMagnitude2 = (l2.transform.position - trackingCenter.position).sqrMagnitude;
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
