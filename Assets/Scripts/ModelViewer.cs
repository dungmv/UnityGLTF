using UnityEngine;
using System.Collections;
using System;
using System.Threading;
using System.Threading.Tasks;
using ReadyPlayerMe.AvatarLoader;
using UnityEngine.Networking;
using GLTFast;
using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine.XR;

public class ModelViewer : MonoBehaviour
{
    // Use this for initialization
    void Start()
    {
        Load("https://models.readyplayer.me/6423ac9aa9cf14ab7e456f88.glb");
    }

    //// Update is called once per frame
    //void Update()
    //{

    //}

    public void Load(string url)
	{
        StartCoroutine(DownloadAvatar(url));
    }

    IEnumerator DownloadAvatar(string url)
    {
        using (var webRequest = UnityWebRequest.Get(url))
        {
            yield return webRequest.SendWebRequest();

            if (!string.IsNullOrWhiteSpace(webRequest.error))
            {
                Debug.LogError($"Error {webRequest.responseCode} - {webRequest.error}");
                yield break;
            }
            ImportAvatar(webRequest.downloadHandler.data, url);
        }
    }

    public async void ImportAvatar(byte[] bytes, string url)
    {
        GltfImport gltf = new GltfImport();
        if (await gltf.LoadGltfBinary(bytes, new Uri(url)))
        {
            GameObject avatar = new GameObject("Avatar");
            avatar.SetActive(true);
            GltFastGameObjectInstantiator customInstantiator = new GltFastGameObjectInstantiator(gltf, avatar.transform);
            await gltf.InstantiateMainSceneAsync(customInstantiator);
        }
    }

    public void RunAnimation(string animationName)
	{

	}
}

