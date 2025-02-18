﻿using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class DetailPanel : MonoBehaviour
{
	public bool shouldClose;

	public Text videoLength;
	public Image thumb;
	public Text title;
	public Text description;
	public Text author;
	public Text timestamp;
	public Text downloadSize;
	public VideoSerialize video;
	public Button playButton;
	public Button playInVRButton;
	public Button downloadButton;
	public Button downloadingButton;
	public Button deleteButton;

	public bool answered;
	public string answerVideoId;
	public bool answerEnableVR;

	private UnityWebRequest imageDownload;
	private GameObject indexPanel;
	private float time;
	private const float refreshTime = 1.0f;

	void Update()
	{
		time += Time.deltaTime;
		if (time > refreshTime)
		{
			Refresh();
			time = 0;
		}

		if (Input.GetKeyDown(KeyCode.Escape))
		{
			Back();
		}
	}

	public IEnumerator Init(VideoSerialize videoToDownload, GameObject indexPanel, bool isLocal)
	{
		this.indexPanel = indexPanel;
		indexPanel.GetComponent<IndexPanel>().modalBackground.SetActive(true);

		video = videoToDownload;

		videoLength.text = MathHelper.FormatSeconds(video.length);
		title.text = video.title;
		description.text = video.description;
		author.text = video.username;
		timestamp.text = MathHelper.FormatTimestampToTimeAgo(video.realTimestamp);
		downloadSize.text = MathHelper.FormatBytes(video.downloadsize);

		if (video.title == "Corrupted file")
		{
			playButton.interactable = false;
			playInVRButton.interactable = false;
		}

		imageDownload = isLocal 
			? UnityWebRequest.Get("file://" + Path.Combine(Application.persistentDataPath, video.id, SaveFile.thumbFilename)) 
			: UnityWebRequest.Get(Web.thumbnailUrl + "?id=" + video.id);

		using (imageDownload)
		{
			yield return imageDownload.SendWebRequest();

			if (imageDownload.result != UnityWebRequest.Result.Success)
			{
				Debug.LogError("Failed to download thumbnail: " + imageDownload.error);
				imageDownload.Dispose();
				imageDownload = null;
			}
			else if (imageDownload.isDone || imageDownload.downloadProgress >= 1f)
			{
				var texture = new Texture2D(1, 1);
				texture.LoadImage(imageDownload.downloadHandler.data);
				thumb.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
				imageDownload.Dispose();
				imageDownload = null;
				thumb.color = Color.white;
			}

			Refresh();
		}
	}
	
	public void Refresh()
	{
		//NOTE(Simon): This happens if a local video was deleted. In that case there is no more info to show, so hide DetailPanel
		if (video == null)
		{
			Back();
			return;
		}

		bool directoryExists = Directory.Exists(Path.Combine(Application.persistentDataPath, video.id));
		bool isDownloading = VideoDownloadManager.Main.GetDownload(video.id) != null;

		bool downloaded = directoryExists && !isDownloading;

		downloadingButton.gameObject.SetActive(!downloaded && isDownloading);
		downloadButton.gameObject.SetActive(!downloaded && !isDownloading);
		playButton.gameObject.SetActive(downloaded);
		playInVRButton.gameObject.SetActive(downloaded);
		deleteButton.gameObject.SetActive(downloaded);
	}

	public void Back()
	{
		indexPanel.SetActive(true);
		indexPanel.GetComponent<IndexPanel>().modalBackground.SetActive(false);
		shouldClose = true;
	}

	public void Play()
	{
		answered = true;
		answerVideoId = video.id;
		answerEnableVR = false;
		indexPanel.SetActive(true);
		indexPanel.GetComponent<IndexPanel>().modalBackground.SetActive(false);
		QualitySettings.vSyncCount = 1;
	}

	public void PlayInVR()
	{
		answered = true;
		answerVideoId = video.id;
		answerEnableVR = true;
		indexPanel.SetActive(true);
		indexPanel.GetComponent<IndexPanel>().modalBackground.SetActive(false);
		QualitySettings.vSyncCount = 0;
	}

	public void Delete()
	{
		string path = Path.Combine(Application.persistentDataPath, video.id);
		bool downloaded = Directory.Exists(path);
		if (downloaded)
		{
			Directory.Delete(path, true);
		}
		//NOTE(Simon): After deleting we should check if this video exists on the server. Refresh() handles updating display
		UpdateVideoFromWeb(video.id);
		Refresh();
	}

	public void Download()
	{
		VideoDownloadManager.Main.AddDownload(video);
	}

	public void UpdateVideoFromWeb(string id)
	{
		var url = $"{Web.videoApiUrl}?id={id}";
		using (var request = UnityWebRequest.Get(url))
		{
			request.SendWebRequest();
		
			while (!request.isDone)
			{
			}

			if (request.result != UnityWebRequest.Result.Success)
			{
				video = null;
			}
			else
			{
				video = JsonUtility.FromJson<VideoSerialize>(request.downloadHandler.text);
			}
		}
	}
}
