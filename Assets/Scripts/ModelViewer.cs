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
using ReadyPlayerMe.Core;

// sample avatar
// Feminine https://models.readyplayer.me/645938677cf7d03f60e0b4e3.glb /1.73628
// Masculine https://models.readyplayer.me/6423ac9aa9cf14ab7e456f88.glb /1.86452
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

    private IEnumerator DownloadAvatar(string url)
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

    private async void ImportAvatar(byte[] bytes, string url)
    {
        GltfImport gltf = new GltfImport();
        if (await gltf.LoadGltfBinary(bytes, new Uri(url)))
        {
            GameObject avatar = new GameObject("Avatar");
            avatar.SetActive(true);
            GltFastGameObjectInstantiator customInstantiator = new GltFastGameObjectInstantiator(gltf, avatar.transform);
            await gltf.InstantiateMainSceneAsync(customInstantiator);
            SetupAnimator(avatar);
        }
    }

    private void SetupAnimator(GameObject model)
    {
        var gender = DetectGender(model);
        string text = ((gender == OutfitGender.Masculine) ? "AnimationAvatars/MasculineAnimationAvatar" : "AnimationAvatars/FeminineAnimationAvatar");
        Animator val = model.AddComponent<Animator>();
        val.avatar = Resources.Load<Avatar>(text);
        val.applyRootMotion = true;
    }

    private OutfitGender DetectGender(GameObject avatar)
    {
        var headTop = avatar.transform.Find("Armature/Hips/Spine/Spine1/Spine2/Neck/Head/HeadTop_End");
        Debug.Log(headTop.position.y);
        return headTop.position.y > 1.8f ? OutfitGender.Masculine : OutfitGender.Feminine;
    }

    public void RunAnimation(string animationName)
	{

	}
}

