﻿using System.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.EventSystems;

[ExecuteInEditMode]
public class UISphere : MonoBehaviour
{
	public float offset;
	private Material material;
	private SphereUIInputModule inputModule;
	private CanvasGroup canvasGroup;
	private float fadeTimer;
	private float fadeLength = .15f;

	void Start()
	{
		if (material == null)
		{
			material = new Material(GetComponent<Renderer>().sharedMaterial);
			GetComponent<Renderer>().material = material;
		}

		if (Application.isPlaying)
		{
			inputModule = FindObjectOfType<SphereUIInputModule>();
			GenerateSphere(2);
		}
	}

	void Update()
	{
		if (Application.isPlaying)
		{
			offset %= 360;
			transform.localRotation = Quaternion.Euler(0, offset, 0);
			var rotation = (transform.localRotation.eulerAngles.y + 270) % 360;
			material.SetFloat("offsetDegrees", rotation);
			Assert.IsNotNull(inputModule);
			inputModule.offset = offset;
		}
	}

	private IEnumerator FadeIn(float delaySeconds = 0)
	{
		fadeTimer = -delaySeconds;
		if (canvasGroup == null)
		{
			canvasGroup = Canvass.sphereUIPanelWrapper.GetComponent<CanvasGroup>();
		}

		while (fadeTimer < fadeLength)
		{
			fadeTimer += Time.deltaTime;
			canvasGroup.alpha = fadeTimer / fadeLength;
			yield return new WaitForEndOfFrame();
		}
		yield return null;
	}

	private IEnumerator FadeOut()
	{
		fadeTimer = 0;
		if (canvasGroup == null)
		{
			canvasGroup = Canvass.sphereUIPanelWrapper.GetComponent<CanvasGroup>();
		}

		while (fadeTimer < fadeLength)
		{
			fadeTimer += Time.deltaTime;
			canvasGroup.alpha = 1 - fadeTimer / fadeLength;
			yield return new WaitForEndOfFrame();
		}
		Canvass.sphereUIWrapper.SetActive(false);
		Canvass.sphereUIRenderer.SetActive(false);
		Canvass.sphereUIWrapper.GetComponentInChildren<EventSystem>().enabled = true;
		yield return null;
	}

	public void Activate(float offset)
	{
		Canvass.sphereUIWrapper.SetActive(true);
		Canvass.sphereUIRenderer.SetActive(true);
		this.offset = offset;
		StartCoroutine(FadeIn());
	}

	public void Deactivate()
	{
		//NOTE(Simon): Deactivate the eventsystem while animating, so we don't have two eventsystems active at once.
		Canvass.sphereUIWrapper.GetComponentInChildren<EventSystem>().enabled = false;
		StartCoroutine(FadeOut());
	}

	public void Suspend()
	{
		Canvass.sphereUIRenderer.SetActive(false);
		//NOTE(Simon): We can't fully deactivate the Sphere Canvas GO, because a child script might still need processing.
		Canvass.sphereUICanvas.GetComponent<Canvas>().enabled = false;
	}

	public void Unsuspend()
	{
		Canvass.sphereUIRenderer.SetActive(true);
		Canvass.sphereUICanvas.GetComponent<Canvas>().enabled = true;
		StartCoroutine(FadeIn(1));
	}

	//NOTE(Simon): From http://wiki.unity3d.com/index.php/ProceduralPrimitives
	void GenerateSphere(int recursionLevel)
	{
		var filter = GetComponent<MeshFilter>();
		var mesh = filter.mesh;

		GenerateUvSphere(ref mesh, 6 * recursionLevel, 6 * recursionLevel);

		mesh.RecalculateBounds();
		mesh.Optimize();
		filter.mesh = mesh;
	}

	void GenerateUvSphere(ref Mesh mesh, int latSubdivisions, int longSubdivisions)
	{
		float radius = 1f;
		// Longitude |||
		int longitude = longSubdivisions;
		// Latitude ---
		int latitude = latSubdivisions;

		var vertices = new Vector3[(longitude + 1) * latitude + 2];
		const float pi = Mathf.PI;
		const float pi2 = pi * 2f;

		vertices[0] = Vector3.up * radius;
		for (int lat = 0; lat < latitude; lat++)
		{
			float a1 = pi * (lat + 1) / (latitude + 1);
			float sin1 = Mathf.Sin(a1);
			float cos1 = Mathf.Cos(a1);

			for (int lon = 0; lon <= longitude; lon++)
			{
				float a2 = pi2 * (lon == longitude ? 0 : lon) / longitude;
				float sin2 = Mathf.Sin(a2);
				float cos2 = Mathf.Cos(a2);

				vertices[lon + lat * (longitude + 1) + 1] = new Vector3(sin1 * cos2, cos1, sin1 * sin2) * radius;
			}
		}

		vertices[vertices.Length - 1] = Vector3.up * -radius;

		var normals = new Vector3[vertices.Length];
		for (int n = 0; n < vertices.Length; n++)
		{
			normals[n] = vertices[n].normalized;
		}

		var uvs = new Vector2[vertices.Length];
		uvs[0] = Vector2.up;
		uvs[uvs.Length - 1] = Vector2.zero;
		for (int lat = 0; lat < latitude; lat++)
		{
			for (int lon = 0; lon <= longitude; lon++)
			{
				uvs[lon + lat * (longitude + 1) + 1] = new Vector2((float)lon / longitude, 1f - (float)(lat + 1) / (latitude + 1));
			}
		}

		int faces = vertices.Length;
		int numtris = faces * 2;
		int indices = numtris * 3;
		var triangles = new int[indices];

		//Top Cap
		int i = 0;
		for (int lon = 0; lon < longitude; lon++)
		{
			triangles[i++] = lon + 2;
			triangles[i++] = lon + 1;
			triangles[i++] = 0;
		}

		//Middle
		for (int lat = 0; lat < latitude - 1; lat++)
		{
			for (int lon = 0; lon < longitude; lon++)
			{
				int current = lon + lat * (longitude + 1) + 1;
				int next = current + longitude + 1;

				triangles[i++] = current;
				triangles[i++] = current + 1;
				triangles[i++] = next + 1;

				triangles[i++] = current;
				triangles[i++] = next + 1;
				triangles[i++] = next;
			}
		}

		//Bottom Cap
		for (int lon = 0; lon < longitude; lon++)
		{
			triangles[i++] = vertices.Length - 1;
			triangles[i++] = vertices.Length - (lon + 2) - 1;
			triangles[i++] = vertices.Length - (lon + 1) - 1;
		}

		mesh.vertices = vertices;
		mesh.normals = normals;
		mesh.uv = uvs;
		mesh.triangles = triangles;
	}
}
