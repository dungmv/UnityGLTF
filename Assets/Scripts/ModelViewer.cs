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
using UnityEngine.Animations;
using UnityEngine.Playables;

// sample avatar
// Feminine https://models.readyplayer.me/645938677cf7d03f60e0b4e3.glb /1.73628
// Masculine https://models.readyplayer.me/6423ac9aa9cf14ab7e456f88.glb /1.86452
public class ModelViewer : MonoBehaviour
{
    [SerializeField] private AnimationClip[] clips;

    private PlayableGraph playableGraph;
    private Animator animator;

    // Use this for initialization
    void Start()
    {
        playableGraph = PlayableGraph.Create();
        Load("https://models.readyplayer.me/6423ac9aa9cf14ab7e456f88.glb");
    }

    private void OnDestroy()
    {
        playableGraph.Destroy();
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
        AvatarBuilder.BuildHumanAvatar(model, new HumanDescription() { });
        var gender = DetectGender(model);
        string text = ((gender == OutfitGender.Masculine) ? "AnimationAvatars/MasculineAnimationAvatar" : "AnimationAvatars/FeminineAnimationAvatar");
        Animator val = model.AddComponent<Animator>();
        val.avatar = Resources.Load<Avatar>(text);
        val.applyRootMotion = true;

        // setup animator
        animator = model.GetComponent<Animator>();
        animator.applyRootMotion = false;
    }

    private OutfitGender DetectGender(GameObject avatar)
    {
        var headTop = avatar.transform.Find("Armature/Hips/Spine/Spine1/Spine2/Neck/Head/HeadTop_End");
        Debug.Log(headTop.position.y);
        return headTop.position.y > 1.8f ? OutfitGender.Masculine : OutfitGender.Feminine;
    }

    public void RunAnimation(int id)
	{
        var clip = clips[id];
        var playableOutput = AnimationPlayableOutput.Create(playableGraph, "AnimationClip", animator);
        // Wrap the clip in a playable
        clip.wrapMode = WrapMode.Loop;
        var playableClip = AnimationClipPlayable.Create(playableGraph, clip);
        // Connect the Playable to an output
        playableOutput.SetSourcePlayable(playableClip);
        // Plays the Graph.
        playableGraph.Play();
    }
}

