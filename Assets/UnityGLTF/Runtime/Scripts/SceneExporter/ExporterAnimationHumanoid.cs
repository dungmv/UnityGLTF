#define ANIMATION_SUPPORTED
#define ANIMATION_EXPORT_SUPPORTED

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityGLTF.Timeline;

namespace UnityGLTF
{
	public partial class GLTFSceneExporter
	{
#if ANIMATION_SUPPORTED
		internal void CollectClipCurvesBySampling(GameObject root, AnimationClip clip, Dictionary<string, TargetCurveSet> targetCurves)
		{
			var recorder = new GLTFRecorder(root.transform, false, false, false);
			var playableGraph = PlayableGraph.Create();
			var animationClipPlayable = (Playable) AnimationClipPlayable.Create(playableGraph, clip);

			var playableOutput = AnimationPlayableOutput.Create(playableGraph, "Animation", root.GetComponent<Animator>());
			playableOutput.SetSourcePlayable(animationClipPlayable);
			playableGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

			var timeStep = 1.0f / 30.0f;
			var length = clip.length;
			var time = 0f;

			// TODO not entirely sure if only checking for humanMotion here is correct
			if (clip.isHumanMotion)
			{
				root.transform.localPosition = Vector3.zero;
				root.transform.localRotation = Quaternion.identity;
				// root.transform.localScale = Vector3.one;
			}

			// first frame
			// AnimationMode.SamplePlayableGraph(playableGraph, 0, time);
			playableGraph.Evaluate(0);
			recorder.StartRecording(time);

			while (time + timeStep < length)
			{
				time += timeStep;
				playableGraph.Evaluate(timeStep);
				// AnimationMode.SamplePlayableGraph(playableGraph, 0, time);
				recorder.UpdateRecording(time);
			}

			// last frame
			time = length;
			playableGraph.Evaluate(length - time);
			// AnimationMode.SamplePlayableGraph(playableGraph, 0, time);
			recorder.UpdateRecording(time);

			recorder.EndRecording(out var data);
			playableGraph.Destroy();
			if (data == null || !data.Any()) return;

			string CalculatePath(Transform child, Transform parent)
			{
				if (child == parent) return "";
				if (child.parent == null) return "";
				var parentPath = CalculatePath(child.parent, parent);
				if (!string.IsNullOrEmpty(parentPath)) return parentPath + "/" + child.name;
				return child.name;
			}

			// convert AnimationData back to AnimationCurve (slow)
			// better would be to directly emit the animation here, but then we need to be careful with partial hierarchies
			// and other cases that can go wrong.
			foreach (var kvp in data)
			{
				var curveSet = new TargetCurveSet();
				curveSet.Init();

				var positionTrack = kvp.Value.tracks.FirstOrDefault(x => x.propertyName == "translation");
				if (positionTrack != null)
				{
					var t0 = positionTrack.times;
					var frameData = positionTrack.values;
					var posX = new AnimationCurve(t0.Select((value, index) => new Keyframe((float)value, ((Vector3)frameData[index]).x)).ToArray());
					var posY = new AnimationCurve(t0.Select((value, index) => new Keyframe((float)value, ((Vector3)frameData[index]).y)).ToArray());
					var posZ = new AnimationCurve(t0.Select((value, index) => new Keyframe((float)value, ((Vector3)frameData[index]).z)).ToArray());
					curveSet.translationCurves = new [] { posX, posY, posZ };
				}

				var rotationTrack = kvp.Value.tracks.FirstOrDefault(x => x.propertyName == "rotation");
				if (rotationTrack != null)
				{
					var t1 = rotationTrack.times;
					var frameData = rotationTrack.values;
					var rotX = new AnimationCurve(t1.Select((value, index) => new Keyframe((float)value, ((Quaternion)frameData[index]).x)).ToArray());
					var rotY = new AnimationCurve(t1.Select((value, index) => new Keyframe((float)value, ((Quaternion)frameData[index]).y)).ToArray());
					var rotZ = new AnimationCurve(t1.Select((value, index) => new Keyframe((float)value, ((Quaternion)frameData[index]).z)).ToArray());
					var rotW = new AnimationCurve(t1.Select((value, index) => new Keyframe((float)value, ((Quaternion)frameData[index]).w)).ToArray());
					curveSet.rotationCurves = new [] { rotX, rotY, rotZ, rotW };
				}

				var scaleTrack = kvp.Value.tracks.FirstOrDefault(x => x.propertyName == "scale");
				if (scaleTrack != null)
				{
					var t2 = scaleTrack.times;
					var frameData = scaleTrack.values;
					var sclX = new AnimationCurve(t2.Select((value, index) => new Keyframe((float)value, ((Vector3)frameData[index]).x)).ToArray());
					var sclY = new AnimationCurve(t2.Select((value, index) => new Keyframe((float)value, ((Vector3)frameData[index]).y)).ToArray());
					var sclZ = new AnimationCurve(t2.Select((value, index) => new Keyframe((float)value, ((Vector3)frameData[index]).z)).ToArray());
					curveSet.scaleCurves = new [] { sclX, sclY, sclZ };
				}

				var calculatedPath = CalculatePath(kvp.Key, root.transform);
				targetCurves[calculatedPath] = curveSet;
			}
		}
#endif
	}
}
