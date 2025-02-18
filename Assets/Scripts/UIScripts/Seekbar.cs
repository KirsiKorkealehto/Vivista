﻿using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;

public class Seekbar : MonoBehaviour
{
	public GameObject blipPrefab;

	public VideoController videoController;
	public RectTransform seekbarBackground;
	public RectTransform seekbarPreview;
	public RectTransform seekbarCurrent;
	public GameObject compass;
	public GameObject compassBackground;
	public GameObject compassForeground;
	public Text timeText;
	public Text blipCounter;
	private bool isEditor;

	public Texture iconPlay;
	public Texture iconPause;
	public RawImage playImage;

	private bool isSeekbarOutOfView;
	public bool isVRSeekbar;

	public bool hovering;
	public float minSeekbarHeight = 0.1f;
	public float curSeekbarHeight;
	public float maxSeekbarHeight = 0.33f;
	public float seekbarAnimationDuration = 0.2f;
	public float lastSmoothTime;

	public float timeSinceLastTextUpdate;

	public static List<Seekbar> instances = new List<Seekbar>();
	public static Seekbar instanceVR;
	public static Seekbar instance;

	public static GameObjectPool blipPool;

	void Awake()
	{
		blipPool = new GameObjectPool(blipPrefab, compass.transform);

		if (isVRSeekbar)
		{
			instanceVR = this;
		}
		else
		{
			instance = this;
		}

		if (!instances.Contains(this))
		{
			instances.Add(this);
		}
	}

	public void Start()
	{
		curSeekbarHeight = minSeekbarHeight;
		if (XRSettings.isDeviceActive)
		{
			AttachCompassToSeekbar();
		}

		blipCounter = compass.GetComponentInChildren<Text>();
		isEditor = SceneManager.GetActiveScene().name.Equals("Editor");
	}

	public static void AttachCompassToSeekbar()
	{
		var seekbar = instances.Find(x => x.isVRSeekbar);
		var compass = seekbar.compass;
		if (compass && seekbar)
		{
			compass.transform.SetParent(seekbar.transform);
			compass.transform.localScale = new Vector3(0.5f, 0.5f, 0);
			compass.transform.localPosition = Vector3.zero;
			compass.transform.localEulerAngles = Vector3.zero;
			compass.transform.GetComponent<RectTransform>().anchoredPosition = new Vector3(-16, 16, 0);
		}
	}

	public static void AttachCompassToController(GameObject controllerUI)
	{
		var compass = instances.Find(x => x.isVRSeekbar).compass;
		compass.transform.SetParent(controllerUI.transform);
		compass.transform.localScale = new Vector3(0.001f, 0.001f, 0.001f);
		compass.transform.localPosition = Vector3.zero;
		compass.transform.localEulerAngles = Vector3.zero;
		compass.transform.GetChild(0).gameObject.SetActive(false);

		compass.gameObject.SetActive(true);
	}

	public void Update()
	{
		playImage.texture = videoController.playing ? iconPause : iconPlay;

		//NOTE(Simon): Update time display and handle seekbar hovers
		{
			Vector2 mousePos = Input.mousePosition;
			float maxMousePos = GetComponent<RectTransform>().rect.width;
			float timeAtMouse = mousePos.x / maxMousePos;

			hovering = RectTransformUtility.RectangleContainsScreenPoint(seekbarBackground.parent.GetComponent<RectTransform>(), mousePos);
			bool onSeekbar = RectTransformUtility.RectangleContainsScreenPoint(seekbarBackground, mousePos);

			float newHeight = hovering
				? curSeekbarHeight + ((maxSeekbarHeight - minSeekbarHeight) * (Time.deltaTime / seekbarAnimationDuration))
				: curSeekbarHeight - ((maxSeekbarHeight - minSeekbarHeight) * (Time.deltaTime / seekbarAnimationDuration));

			float smoothedTime = Mathf.Lerp(lastSmoothTime, (float) videoController.currentFractionalTime, 0.5f);

			curSeekbarHeight = Mathf.Clamp(newHeight, minSeekbarHeight, maxSeekbarHeight);
			seekbarCurrent.anchorMax = new Vector2(smoothedTime, seekbarCurrent.anchorMax.y);
			seekbarBackground.anchorMax = new Vector2(seekbarBackground.anchorMax.x, curSeekbarHeight);
			seekbarPreview.anchorMax = new Vector2(onSeekbar ? timeAtMouse : 0, seekbarPreview.anchorMax.y);

			if (onSeekbar)
			{
				timeText.text = $" {MathHelper.FormatSeconds(videoController.TimeForFraction(timeAtMouse))} / {MathHelper.FormatSeconds(videoController.videoLength)}";
			}
			else if (timeSinceLastTextUpdate > 0.5)
			{
				timeText.text = $" {MathHelper.FormatSeconds(videoController.rawCurrentTime)} / {MathHelper.FormatSeconds(videoController.videoLength)}";
				timeSinceLastTextUpdate = 0;
			}

			timeSinceLastTextUpdate += Time.deltaTime;
			lastSmoothTime = smoothedTime;
		}

		//NOTE(Simon): Update compass rotation
		{
			// TODO(Lander): Actually make use of the start position, and no hardcoded values
			float rotation = Camera.main.transform.rotation.eulerAngles.y + 0;
			if (!isEditor && isVRSeekbar)
			{
				rotation -= 90;
			}

			compassForeground.transform.localEulerAngles = new Vector3(0, 0, -(rotation));
		}

		//NOTE(Kristof): Rotating the seekbar
		if (XRSettings.isDeviceActive && isVRSeekbar)
		{
			//NOTE(Kristof): Seekbar rotation is the same as the seekbar's angle on the circle
			var seekbarAngle = Vector2.SignedAngle(new Vector2(transform.position.x, transform.position.z), Vector2.up);

			var fov = Camera.main.fieldOfView;
			//NOTE(Kristof): Camera rotation tells you to which angle on the circle the camera is looking towards
			var cameraAngle = Camera.main.transform.eulerAngles.y;

			//NOTE(Kristof): Calculate the absolute degree angle from the camera to the seekbar
			var distanceLeft = Mathf.Abs((cameraAngle - seekbarAngle + 360) % 360);
			var distanceRight = Mathf.Abs((cameraAngle - seekbarAngle - 360) % 360);

			var angle = Mathf.Min(distanceLeft, distanceRight);

			if (isSeekbarOutOfView)
			{
				if (angle < 2.5f)
				{
					isSeekbarOutOfView = false;
				}
			}
			else
			{
				if (angle > fov)
				{
					isSeekbarOutOfView = true;
				}
			}

			if (isSeekbarOutOfView)
			{
				float newAngle = Mathf.LerpAngle(seekbarAngle, cameraAngle, 0.025f);

				//NOTE(Kristof): Angle needs to be reversed, in Unity postive angles go clockwise while they go counterclockwise in the unit circle (cos and sin)
				//NOTE(Kristof): We also need to add an offset of 90 degrees because in Unity 0 degrees is in front of you, in the unit circle it is (1,0) on the axis
				float radianAngle = (-newAngle + 90) * Mathf.PI / 180;
				float x = 1.8f * Mathf.Cos(radianAngle);
				float y = Camera.main.transform.position.y - 2f;
				float z = 1.8f * Mathf.Sin(radianAngle);

				transform.position = new Vector3(x, y, z);
				transform.eulerAngles = new Vector3(30, newAngle, 0);
			}
		}
	}

	public void OnPointerUp()
	{
		var pos = Input.mousePosition.x;
		var max = GetComponent<RectTransform>().rect.width;

		var fractionalTime = pos / max;

		videoController.SeekFractional(fractionalTime);
	}

	public void Skip(float amount)
	{
		videoController.SeekRelative(amount);
	}

	public void TogglePlay()
	{
		videoController.TogglePlay();
	}

	public static void ClearBlips()
	{
		blipPool.ClearActive();
	}

	public void RenderBlips(List<InteractionPointPlayer> activeInteractionPoints)
	{
		var blipsToShow = new List<InteractionPointPlayer>();

		//NOTE(Simon): Count active points
		for (int i = 0; i < activeInteractionPoints.Count; i++)
		{
			if (!activeInteractionPoints[i].isSeen)
			{
				blipsToShow.Add(activeInteractionPoints[i]);
			}
		}

		//NOTE(Simon): Update counter text
		blipCounter.text = blipPool.Count != 0
			? blipPool.Count.ToString()
			: "";

		blipPool.EnsureActiveCount(blipsToShow.Count);

		//NOTE(Simon): Update blips, and active/create new blips when necessary
		for (int i = 0; i < blipsToShow.Count; i++)
		{
			var point = blipsToShow[i];

			float blipAngle = point.point.transform.eulerAngles.y;
			float angle = (XRSettings.isDeviceActive ? 90 : compass.transform.localEulerAngles.y) - blipAngle;
			blipPool[i].transform.localEulerAngles = new Vector3(0, 0, angle);
			blipPool[i].transform.SetParent(compassBackground.transform, false);
		}
	}
}
