#define ANIMATION_EXPORT_SUPPORTED
#define ANIMATION_SUPPORTED
#undef USE_ANIMATION_POINTER

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GLTF.Schema;
using UnityEngine;
using UnityGLTF.Extensions;
using UnityEngine.Animations;
using Object = UnityEngine.Object;

namespace UnityGLTF
{
	public partial class GLTFSceneExporter
	{
		private readonly Dictionary<(AnimationClip clip, float speed), GLTFAnimation> _clipToAnimation = new Dictionary<(AnimationClip, float), GLTFAnimation>();
		private readonly Dictionary<(AnimationClip clip, float speed, string targetPath), Transform> _clipAndSpeedAndPathToExportedTransform = new Dictionary<(AnimationClip, float, string), Transform>();

#if ANIMATION_SUPPORTED
		private static int AnimationBakingFramerate = 30; // FPS
		private static bool BakeAnimationData = true;
#endif

		// Parses Animation/Animator component and generate a glTF animation for the active clip
		// This may need additional work to fully support animatorControllers
		public void ExportAnimationFromNode(ref Transform transform)
		{
			exportAnimationFromNodeMarker.Begin();

//#if ANIMATION_SUPPORTED
//			Animator animator = transform.GetComponent<Animator>();
//			if (animator)
//			{
//#if ANIMATION_EXPORT_SUPPORTED
//                AnimationClip[] clips = AnimationUtility.GetAnimationClips(transform.gameObject);
//                var animatorController = animator.runtimeAnimatorController as AnimatorController;
//				// Debug.Log("animator: " + animator + "=> " + animatorController);
//                ExportAnimationClips(transform, clips, animator, animatorController);
//#endif
//			}

//			UnityEngine.Animation animation = transform.GetComponent<UnityEngine.Animation>();
//			if (animation)
//			{
//#if ANIMATION_EXPORT_SUPPORTED
//                AnimationClip[] clips = AnimationUtility.GetAnimationClips(transform.gameObject);
//                ExportAnimationClips(transform, clips);
//#endif
//			}
//#endif
			exportAnimationFromNodeMarker.End();
		}

		/* 
		 * new anim here
		 */
        private GLTFAnimation GetOrCreateAnimation(AnimationClip clip, string searchForDuplicateName, float speed)
        {
            var existingAnim = default(GLTFAnimation);
            if (_exportOptions.MergeClipsWithMatchingNames)
            {
                // Check if we already exported an animation with exactly that name. If yes, we want to append to the previous one instead of making a new one.
                // This allows to merge multiple animations into one if required (e.g. a character and an instrument that should play at the same time but have individual clips).
                existingAnim = _root.Animations?.FirstOrDefault(x => x.Name == searchForDuplicateName);
            }

            // TODO when multiple AnimationClips are exported, we're currently not properly merging those;
            // we should only export the GLTFAnimation once but then apply that to all nodes that require it (duplicating the animation but not the accessors)
            // instead of naively writing over the GLTFAnimation with the same data.
            var animationClipAndSpeed = (clip, speed);
            if (existingAnim == null)
            {
                if (_clipToAnimation.TryGetValue(animationClipAndSpeed, out existingAnim))
                {
                    // we duplicate the clip it was exported before so we can retarget to another transform.
                    existingAnim = new GLTFAnimation(existingAnim, _root);
                }
            }

            GLTFAnimation anim = existingAnim != null ? existingAnim : new GLTFAnimation();

            // add to set of already exported clip-state pairs
            if (!_clipToAnimation.ContainsKey(animationClipAndSpeed))
                _clipToAnimation.Add(animationClipAndSpeed, anim);

            return anim;
        }

        public GLTFAnimation ExportAnimationClip(AnimationClip clip, string name, Transform node, float speed)
		{
			if (!clip) return null;
			GLTFAnimation anim = GetOrCreateAnimation(clip, name, speed);

			anim.Name = name;

			ConvertClipToGLTFAnimation(clip, node, anim, speed);

			if (anim.Channels.Count > 0 && anim.Samplers.Count > 0 && !_root.Animations.Contains(anim))
			{
				_root.Animations.Add(anim);
				_animationClips.Add((node, clip));
			}
			return anim;
		}

#if ANIMATION_SUPPORTED
		public class PropertyCurve
		{
			public string propertyName;
			public Type propertyType;
			public List<AnimationCurve> curve;
			public List<string> curveName;
			public Object target;

			public PropertyCurve(Object target, string propertyName)
			{
				this.target = target;
				this.propertyName = propertyName;
				curve = new List<AnimationCurve>();
				curveName = new List<string>();
			}

			public void AddCurve(AnimationCurve animCurve, string name)
			{
				this.curve.Add(animCurve);
				this.curveName.Add(name);
			}

			public float Evaluate(float time, int index)
			{
				if (index < 0 || index >= curve.Count)
				{
					// common case: A not animated but RGB is.
					// TODO this should actually use the value from the material.
					if (propertyType == typeof(Color) && index == 3)
						return 1;

					throw new ArgumentOutOfRangeException(nameof(index), $"PropertyCurve {propertyName} ({propertyType}) has only {curve.Count} curves but index {index} was accessed for time {time}");
				}

				return curve[index].Evaluate(time);
			}

			internal bool Validate()
			{
				if (propertyType == typeof(Color))
				{
					var hasEnoughCurves = curve.Count == 4;
					if (!hasEnoughCurves)
					{
						UnityEngine.Debug.LogWarning("Animating single channels for colors is not supported. Please add at least one keyframe for all channels (RGBA): " + propertyName, target);
						return false;
					}
				}

				return true;
			}

			/// <summary>
			/// Call this method once before beginning to evaluate curves
			/// </summary>
			internal void SortCurves()
			{
				// If we animate a color property in Unity and start by creating keys for green then the green curve will be at index 0
				// This method ensures that the curves are in a known order e.g. rgba (instead of green red blue alpha)
				if (curve?.Count > 0 && curveName.Count > 0)
				{
					if (propertyType == typeof(Color))
					{
						FillTempLists();
						var indexOfR = FindIndex(name => name.EndsWith(".r"));
						var indexOfG = FindIndex(name => name.EndsWith(".g"));
						var indexOfB = FindIndex(name => name.EndsWith(".b"));
						var indexOfA = FindIndex(name => name.EndsWith(".a"));
						for(var i = 0; i < curve.Count; i++)
						{
							var curveIndex = i;
							if (i == 0) curveIndex = indexOfR;
							else if (i == 1) curveIndex = indexOfG;
							else if (i == 2) curveIndex = indexOfB;
							else if (i == 3) curveIndex = indexOfA;
							if (curveIndex >= 0 && curveIndex != i)
							{
								this.curve[i] = _tempList1[curveIndex];;
								this.curveName[i] = _tempList2[curveIndex];;
							}
						}
					}
				}
			}

			private static readonly List<AnimationCurve> _tempList1 = new List<AnimationCurve>();
			private static readonly List<string> _tempList2 = new List<string>();

			private void FillTempLists()
			{
				_tempList1.Clear();
				_tempList2.Clear();
				_tempList1.AddRange(curve);
				_tempList2.AddRange(curveName);
			}

			public int FindIndex(Predicate<string> test)
			{
				for(var i = 0; i < curveName.Count; i++)
				{
					if (test(curveName[i]))
						return i;
				}
				return -1;
			}


		}

		internal struct TargetCurveSet
		{
			#pragma warning disable 0649
			public AnimationCurve[] translationCurves;
			public AnimationCurve[] rotationCurves;
			public AnimationCurve[] scaleCurves;
			public Dictionary<string, AnimationCurve> weightCurves;
			public PropertyCurve propertyCurve;
			#pragma warning restore

			public Dictionary<string, PropertyCurve> propertyCurves;

			public void Init()
			{
				translationCurves = new AnimationCurve[3];
				rotationCurves = new AnimationCurve[4];
				scaleCurves = new AnimationCurve[3];
				weightCurves = new Dictionary<string, AnimationCurve>();
			}
        }

        private static string LogObject(object obj)
		{
			if (obj == null) return "null";

			if (obj is Component tr)
				return $"{tr.name} (InstanceID: {tr.GetInstanceID()}, Type: {tr.GetType()})";
			if (obj is GameObject go)
				return $"{go.name} (InstanceID: {go.GetInstanceID()})";

			return obj.ToString();
		}

		private Dictionary<(Object key, AnimationClip clip, float speed), AnimationClip> _sampledClipInstanceCache = new Dictionary<(Object, AnimationClip, float), AnimationClip>();

		private bool ClipRequiresSampling(AnimationClip clip, Transform transform)
		{
			var clipRequiresSampling = clip.isHumanMotion;

			// we also need to bake if this Animator uses animation rigging for dynamic motion
			var haveAnyRigComponents = transform.GetComponents<IAnimationWindowPreview>().Any(x => ((Behaviour)x).enabled);
			if (haveAnyRigComponents) clipRequiresSampling = true;

			return clipRequiresSampling;
		}

		private void ConvertClipToGLTFAnimation(AnimationClip clip, Transform transform, GLTFAnimation animation, float speed)
		{
			convertClipToGLTFAnimationMarker.Begin();

			// Generate GLTF.Schema.AnimationChannel and GLTF.Schema.AnimationSampler
			// 1 channel per node T/R/S, one sampler per node T/R/S
			// Need to keep a list of nodes to convert to indexes

			// Special case for animated humanoids: we also need to cache transform-to-humanoid and make sure that individual clips are used there.
			// since we're baking humanoids, we'd otherwise end up with the same clip being applied to different rigs;
			// in the future, we may want to support a system like VRM or EXT_skin_humanoid (https://github.com/takahirox/EXT_skin_humanoid) and support runtime retargeting of animations.
			if (ClipRequiresSampling(clip, transform))
			{
				var animator = transform.GetComponent<Animator>();
				var avatar = animator.avatar;
				Object instanceCacheKey = avatar;

				if (clip.isHumanMotion && !avatar)
				{
					Debug.LogWarning(null, $"No avatar found on animated humanoid, skipping humanoid animation export on {transform.name}", transform);
					convertClipToGLTFAnimationMarker.End();
					return;
				}
				var key = (instanceCacheKey, clip, speed);
				if(!_sampledClipInstanceCache.ContainsKey(key))
					_sampledClipInstanceCache.Add(key, Object.Instantiate(clip));
				clip = _sampledClipInstanceCache[key];
			}

			// 1. browse clip, collect all curves and create a TargetCurveSet for each target
			Dictionary<string, TargetCurveSet> targetCurvesBinding = new Dictionary<string, TargetCurveSet>();
			CollectClipCurves(transform.gameObject, clip, targetCurvesBinding);

			// Baking needs all properties, fill missing curves with transform data in 2 keyframes (start, endTime)
			// where endTime is clip duration
			// Note: we should avoid creating curves for a property if none of it's components is animated

			GenerateMissingCurves(clip.length, transform, ref targetCurvesBinding);

			if (BakeAnimationData)
			{
				// Bake animation for all animated nodes
				foreach (string target in targetCurvesBinding.Keys)
				{
					var hadAlreadyExportedThisBindingBefore = _clipAndSpeedAndPathToExportedTransform.TryGetValue((clip, speed, target), out var alreadyExportedTransform);
					Transform targetTr = target.Length > 0 ? transform.Find(target) : transform;
					int newTargetId = targetTr ? GetTransformIndex(targetTr) : -1;

					var targetTrShouldNotBeExported = targetTr && !targetTr.gameObject.activeInHierarchy && !settings.ExportDisabledGameObjects;

					if (hadAlreadyExportedThisBindingBefore && newTargetId < 0)
					{
						// warn: the transform for this binding exists, but its Node isn't exported. It's probably disabled and "Export Disabled" is off.
						if (targetTr)
						{
							Debug.LogWarning("An animated transform is not part of _exportedTransforms, is the object disabled? " + LogObject(targetTr), targetTr);
						}

						// we need to remove the channels and samplers from the existing animation that was passed in if they exist
						int alreadyExportedChannelTargetId = GetTransformIndex(alreadyExportedTransform);
						animation.Channels.RemoveAll(x => x.Target.Node != null && x.Target.Node.Id == alreadyExportedChannelTargetId);

						if (settings.UseAnimationPointer)
						{
							animation.Channels.RemoveAll(x =>
							{
								if (x.Target.Extensions != null && x.Target.Extensions.TryGetValue(KHR_animation_pointer.EXTENSION_NAME, out var ext) && ext is KHR_animation_pointer animationPointer)
								{
									var obj = animationPointer.animatedObject;
									if (obj is Component c)
										obj = c.transform;
									if (obj is Transform tr2 && tr2 == alreadyExportedTransform)
										return true;
								}
								return false;
							});
						}

						// TODO remove all samplers from this animation that were targeting the channels that we just removed
						// TODO: this doesn't work because we're punching holes in the sampler order; all channel sampler IDs would need to be adjusted as well.

						continue;
					}

					if (hadAlreadyExportedThisBindingBefore)
					{
						int alreadyExportedChannelTargetId = GetTransformIndex(alreadyExportedTransform);

						for (int i = 0; i < animation.Channels.Count; i++)
						{
							var existingTarget = animation.Channels[i].Target;
							if (existingTarget.Node != null && existingTarget.Node.Id != alreadyExportedChannelTargetId) continue;

							// if we're here it means that an existing AnimationChannel already targets the same node that we're currently targeting.
							// Without KHR_animation_pointer, that just means we reuse the existing data and tell it to target a new node.
							// With KHR_animation_pointer, we need to do the same, and retarget the path to the new node.
							if (existingTarget.Extensions != null && existingTarget.Extensions.TryGetValue(KHR_animation_pointer.EXTENSION_NAME, out var ext) && ext is KHR_animation_pointer animationPointer)
							{
								// Debug.Log($"export? {!targetTrShouldNotBeExported} - {nameof(existingTarget)}: {L(existingTarget)}, {nameof(animationPointer)}: {L(animationPointer.animatedObject)}, {nameof(alreadyExportedTransform)}: {L(alreadyExportedTransform)}, {nameof(targetTr)}: {L(targetTr)}");
								var obj = animationPointer.animatedObject;
								Transform animatedTransform = default;
								if (obj is Component comp) animatedTransform = comp.transform;
								else if (obj is GameObject go) animatedTransform = go.transform;
								if (animatedTransform == alreadyExportedTransform)
								{
									if (targetTrShouldNotBeExported)
									{
										// Debug.LogWarning("Need to remove this", null);
									}
									else
									{
										if (animationPointer.animatedObject is GameObject)
										{
											animationPointer.animatedObject = targetTr.gameObject;
											animationPointer.channel = existingTarget;
											animationPointerResolver.Add(animationPointer);
										}
										else if(animationPointer.animatedObject is Component)
										{
											var targetType = animationPointer.animatedObject.GetType();
											var newTarget = targetTr.GetComponent(targetType);
											if (newTarget)
											{
												animationPointer.animatedObject = newTarget;
												animationPointer.channel = existingTarget;
												animationPointerResolver.Add(animationPointer);
											}
										}
									}
								}
								else if (animationPointer.animatedObject is Material m)
								{
									var renderer = targetTr.GetComponent<MeshRenderer>();
									if (renderer)
									{
										// TODO we don't have a good way right now to solve this if there's multiple materials on this renderer...
										// would probably need to keep the clip path / binding around and check if that uses a specific index and so on
										var newTarget = renderer.sharedMaterial;
										if (newTarget)
										{
											animationPointer.animatedObject = newTarget;
											animationPointer.channel = existingTarget;
											animationPointerResolver.Add(animationPointer);
										}
									}
								}
							}
							else if (targetTr)
							{
								existingTarget.Node = new NodeId()
								{
									Id = newTargetId,
									Root = _root
								};
							}
						}
						continue;
					}

					if (targetTrShouldNotBeExported)
					{
						Debug.Log("Object " + targetTr + " is disabled, not exporting animated curve " + target, targetTr);
						continue;
					}

					// add to cache: this is the first time we're exporting that particular binding.
					if (targetTr)
						_clipAndSpeedAndPathToExportedTransform.Add((clip, speed, target), targetTr);

					var curve = targetCurvesBinding[target];
					var speedMultiplier = Mathf.Clamp(speed, 0.01f, Mathf.Infinity);

					// Initialize data
					// Bake and populate animation data
					float[] times = null;

					// arbitrary properties require the KHR_animation_pointer extension
					bool sampledAnimationData = false;
					if (settings.UseAnimationPointer && curve.propertyCurves != null && curve.propertyCurves.Count > 0)
					{
						var curves = curve.propertyCurves;
						foreach (KeyValuePair<string, PropertyCurve> c in curves)
						{
							var prop = c.Value;
							if (BakePropertyAnimation(prop, clip.length, AnimationBakingFramerate, speedMultiplier, out times, out var values))
							{
								AddAnimationData(prop.target, prop.propertyName, animation, times, values);
								sampledAnimationData = true;
							}
						}
					}

					if (targetTr)
					{
					// TODO these should be moved into curve.propertyCurves as well
					// TODO should filter by possible propertyCurve string names at that point to avoid
					// moving KHR_animation_pointer data into regular animations
					if (curve.translationCurves.Any(x => x != null))
					{
						var trp2 = new PropertyCurve(targetTr, "translation") { propertyType = typeof(Vector3) };
						trp2.curve.AddRange(curve.translationCurves);
						if (BakePropertyAnimation(trp2, clip.length, AnimationBakingFramerate, speedMultiplier, out times, out var values2))
						{
							AddAnimationData(targetTr, trp2.propertyName, animation, times, values2);
							sampledAnimationData = true;
						}
					}

					if (curve.rotationCurves.Any(x => x != null))
					{
						var trp3 = new PropertyCurve(targetTr, "rotation") { propertyType = typeof(Quaternion) };
						trp3.curve.AddRange(curve.rotationCurves.Where(x => x != null));
						if (BakePropertyAnimation(trp3, clip.length, AnimationBakingFramerate, speedMultiplier, out times, out var values3))
						{
							AddAnimationData(targetTr, trp3.propertyName, animation, times, values3);
							sampledAnimationData = true;
						}

					}

					if (curve.scaleCurves.Any(x => x != null))
					{
						var trp4 = new PropertyCurve(targetTr, "scale") { propertyType = typeof(Vector3) };
						trp4.curve.AddRange(curve.scaleCurves);
						if (BakePropertyAnimation(trp4, clip.length, AnimationBakingFramerate, speedMultiplier, out times, out var values4))
						{
							AddAnimationData(targetTr, trp4.propertyName, animation, times, values4);
							sampledAnimationData = true;
						}
					}

					if (curve.weightCurves.Any(x => x.Value != null))
					{
						var trp5 = new PropertyCurve(targetTr, "weights") { propertyType = typeof(float) };
						trp5.curve.AddRange(curve.weightCurves.Values);
						if (BakePropertyAnimation(trp5, clip.length, AnimationBakingFramerate, speedMultiplier, out times, out var values5))
						{
							var targetComponent = targetTr.GetComponent<SkinnedMeshRenderer>();
							AddAnimationData(targetComponent, trp5.propertyName, animation, times, values5);
							sampledAnimationData = true;
						}
					}
					}

					if (!sampledAnimationData)
						Debug.LogWarning("Warning: empty animation curves for " + target + " in " + clip + " from " + transform, transform);
				}
			}
			else
			{
				Debug.LogError("Only baked animation is supported for now. Skipping animation", null);
			}

			convertClipToGLTFAnimationMarker.End();
		}

		private void CollectClipCurves(GameObject root, AnimationClip clip, Dictionary<string, TargetCurveSet> targetCurves)
		{
			if (ClipRequiresSampling(clip, root.transform))
			{
				CollectClipCurvesBySampling(root, clip, targetCurves);
				return;
			}

			throw new Exception("Only Animation HumanMotion Support");
		}

        private void GenerateMissingCurves(float endTime, Transform tr, ref Dictionary<string, TargetCurveSet> targetCurvesBinding)
		{
			var keyList = targetCurvesBinding.Keys.ToList();
			foreach (string target in keyList)
			{
				Transform targetTr = target.Length > 0 ? tr.Find(target) : tr;
				if (targetTr == null)
					continue;

				TargetCurveSet current = targetCurvesBinding[target];

				if (current.weightCurves.Count > 0)
				{
					// need to sort and generate the other matching curves as constant curves for all blend shapes
					var renderer = targetTr.GetComponent<SkinnedMeshRenderer>();
					var mesh = renderer.sharedMesh;
					var shapeCount = mesh.blendShapeCount;

					// need to reorder weights: Unity stores the weights alphabetically in the AnimationClip,
					// not in the order of the weights.
					var newWeights = new Dictionary<string, AnimationCurve>();
					for (int i = 0; i < shapeCount; i++)
					{
						var shapeName = mesh.GetBlendShapeName(i);
						var shapeCurve = current.weightCurves.ContainsKey(shapeName) ? current.weightCurves[shapeName] : CreateConstantCurve(renderer.GetBlendShapeWeight(i), endTime);
						newWeights.Add(shapeName, shapeCurve);
					}

					current.weightCurves = newWeights;
				}

				if (current.propertyCurves?.Count > 0)
				{
					foreach (var kvp in current.propertyCurves)
					{
						var prop = kvp.Value;
						if (prop.propertyType == typeof(Color))
						{
							var memberName = prop.propertyName;
							if (TryGetCurrentValue(prop.target, memberName, out var value))
							{
								// Generate missing color channels (so an animated color has always keyframes for all 4 channels)

								var col = (Color)value;

								var hasRedChannel = prop.FindIndex(v => v.EndsWith(".r")) >= 0;
								var hasGreenChannel = prop.FindIndex(v => v.EndsWith(".g")) >= 0;
								var hasBlueChannel = prop.FindIndex(v => v.EndsWith(".b")) >= 0;
								var hasAlphaChannel = prop.FindIndex(v => v.EndsWith(".a")) >= 0;

								if (!hasRedChannel) AddMissingCurve(memberName + ".r", col.r);
								if (!hasGreenChannel) AddMissingCurve(memberName + ".g", col.g);
								if (!hasBlueChannel) AddMissingCurve(memberName + ".b", col.b);
								if (!hasAlphaChannel) AddMissingCurve(memberName + ".a", col.a);

								void AddMissingCurve(string curveName, float constantValue)
								{
									var curve = CreateConstantCurve(constantValue, endTime);
									prop.curve.Add(curve);
									prop.curveName.Add(curveName);
								}
							}
						}
					}
				}

				targetCurvesBinding[target] = current;
			}
		}

		private static readonly Dictionary<(Type type, string name), MemberInfo> memberCache = new Dictionary<(Type type, string name), MemberInfo>();
		private static bool TryGetCurrentValue(object instance, string memberName, out object value)
		{
			if (instance == null || memberName == null)
			{
				value = null;
				return false;
			}

			var key = (instance.GetType(), memberName);
			if (!memberCache.TryGetValue(key, out var member))
			{
				var type = instance.GetType();
				while (type != null)
				{
					member = type
						.GetMember(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
						.FirstOrDefault();
					if (member != null)
					{
						memberCache.Add(key, member);
						break;
					}
					type = type.BaseType;
				}
			}

			if (member == null)
			{
				value = null;
				return false;
			}

			switch (member)
			{
				case FieldInfo field:
					value = field.GetValue(instance);
					return true;
				case PropertyInfo property:
					value = property.GetValue(instance);
					return true;
				default:
					value = null;
					return false;
			}
		}

		private AnimationCurve CreateConstantCurve(float value, float endTime)
		{
			// No translation curves, adding them
			AnimationCurve curve = new AnimationCurve();
			curve.AddKey(0, value);
			curve.AddKey(endTime, value);
			return curve;
		}

		private bool BakePropertyAnimation(PropertyCurve prop, float length, float bakingFramerate, float speedMultiplier, out float[] times, out object[] values)
		{
			times = null;
			values = null;

			if (!prop.Validate()) return false;

			var nbSamples = Mathf.Max(1, Mathf.CeilToInt(length * bakingFramerate));
			var deltaTime = length / nbSamples;

			var _times = new List<float>(nbSamples * 2);
			var _values = new List<object>(nbSamples * 2);

			var curveCount = prop.curve.Count;
			var keyframes = prop.curve.Select(x => x.keys).ToArray();
			var keyframeIndex = new int[curveCount];

			prop.SortCurves();

			var vector3Scale = SchemaExtensions.CoordinateSpaceConversionScale.ToUnityVector3Raw();

			// Assuming all the curves exist now
			for (var i = 0; i < nbSamples; ++i)
			{
				var time = i * deltaTime;
				if (i == nbSamples - 1) time = length;

				for (var k = 0; k < curveCount; k++)
					while (keyframeIndex[k] < keyframes[k].Length - 1 && keyframes[k][keyframeIndex[k]].time < time)
						keyframeIndex[k]++;

				var isConstant = false;
				for (var k = 0; k < curveCount; k++)
					isConstant |= float.IsInfinity(keyframes[k][keyframeIndex[k]].inTangent);

				if (isConstant && _times.Count > 0)
				{
					var lastTime = _times[_times.Count - 1];
					var t0 = lastTime + 0.0001f;
					if (i != nbSamples - 1)
						time += deltaTime * 0.999f;
					_times.Add(t0 / speedMultiplier);
					_times.Add(time / speedMultiplier);
					var success = AddValue(time);
					success &= AddValue(time);
					if (!success) return false;
				}
				else
				{
					var t0 = time / speedMultiplier;
					_times.Add(t0);
					if (!AddValue(t0)) return false;
				}

				bool AddValue(float t)
				{
					if (prop.curve.Count == 1)
					{
						var value = prop.curve[0].Evaluate(t);
						_values.Add(value);
					}
					else
					{
						var type = prop.propertyType;

						if (typeof(Vector2) == type)
						{
							_values.Add(new Vector2(prop.Evaluate(t, 0), prop.Evaluate(t, 1)));
						}
						else if (typeof(Vector3) == type)
						{
							var vec = new Vector3(prop.Evaluate(t, 0), prop.Evaluate(t, 1), prop.Evaluate(t, 2));
							_values.Add(vec);
						}
						else if (typeof(Vector4) == type)
						{
							_values.Add(new Vector4(prop.Evaluate(t, 0), prop.Evaluate(t, 1), prop.Evaluate(t, 2), prop.Evaluate(t, 3)));
						}
						else if (typeof(Color) == type)
						{
							// TODO should actually access r,g,b,a separately since any of these can have curves assigned.
							var r = prop.Evaluate(t, 0);
							var g = prop.Evaluate(t, 1);
							var b = prop.Evaluate(t, 2);
							var a = prop.Evaluate(t, 3);
							_values.Add(new Color(r, g, b, a));
						}
						else if (typeof(Quaternion) == type)
						{
							if (prop.curve.Count == 3)
							{
								Quaternion eulerToQuat = Quaternion.Euler(prop.Evaluate(t, 0), prop.Evaluate(t, 1), prop.Evaluate(t, 2));
								_values.Add(new Quaternion(eulerToQuat.x, eulerToQuat.y, eulerToQuat.z, eulerToQuat.w));
							}
							else if (prop.curve.Count == 4)
							{
								_values.Add(new Quaternion(prop.Evaluate(t, 0), prop.Evaluate(t, 1), prop.Evaluate(t, 2), prop.Evaluate(t, 3)));
							}
							else
							{
								Debug.LogError(null, $"Rotation animation has {prop.curve.Count} curves, expected Euler Angles (3 curves) or Quaternions (4 curves). This is not supported, make sure to animate all components of rotations. Animated object {prop.target}", prop.target);
							}
						}
						else if (typeof(float) == type)
						{
							foreach (var val in prop.curve)
								_values.Add(val.Evaluate(t));
						}
						else
						{
							switch (prop.propertyName)
							{
								case "MotionT":
								case "MotionQ":
									// Ignore
									break;
								default:
									Debug.LogWarning(null, "Property is animated but can't be exported - Name: " + prop.propertyName + ", Type: " + prop.propertyType + ". Does its target exist? You can enable KHR_animation_pointer export in the Project Settings to export more animated properties.");
									break;

							}
							return false;
						}
					}

					return true;
				}
			}

			times = _times.ToArray();
			values = _values.ToArray();

			RemoveUnneededKeyframes(ref times, ref values);

			return true;
		}
#endif

			[Obsolete("Please use " + nameof(GetTransformIndex), false)]
		public int GetNodeIdFromTransform(Transform transform)
		{
			return GetTransformIndex(transform);
		}

		internal int GetIndex(object obj)
		{
			switch (obj)
			{
				case Material m: return GetMaterialIndex(m);
				case Light l: return GetLightIndex(l);
				case Camera c: return GetCameraIndex(c);
				case Transform t: return GetTransformIndex(t);
				case GameObject g: return GetTransformIndex(g.transform);
				case Component k: return GetTransformIndex(k.transform);
			}

			return -1;
		}

		public int GetTransformIndex(Transform transform)
		{
			if (transform && _exportedTransforms.TryGetValue(transform.GetInstanceID(), out var index)) return index;
			return -1;
		}

		public int GetMaterialIndex(Material mat)
		{
			if (mat && _exportedMaterials.TryGetValue(mat.GetInstanceID(), out var index)) return index;
			return -1;
		}

		public int GetLightIndex(Light light)
		{
			if (light && _exportedLights.TryGetValue(light.GetInstanceID(), out var index)) return index;
			return -1;
		}

		public int GetCameraIndex(Camera cam)
		{
			if (cam && _exportedCameras.TryGetValue(cam.GetInstanceID(), out var index)) return index;
			return -1;
		}

		public IEnumerable<(int subMeshIndex, MeshPrimitive prim)> GetPrimitivesForMesh(Mesh mesh)
		{
			if (!_meshToPrims.TryGetValue(mesh, out var prims)) yield break;
			foreach (var k in prims.subMeshPrimitives)
			{
				yield return (k.Key, k.Value);
			}
		}

		private static void DecomposeEmissionColor(Color input, out Color output, out float intensity)
		{
			var emissiveAmount = input.linear;
			var maxEmissiveAmount = Mathf.Max(emissiveAmount.r, emissiveAmount.g, emissiveAmount.b);
			if (maxEmissiveAmount > 1)
			{
				emissiveAmount.r /= maxEmissiveAmount;
				emissiveAmount.g /= maxEmissiveAmount;
				emissiveAmount.b /= maxEmissiveAmount;
			}
			emissiveAmount.a = Mathf.Clamp01(emissiveAmount.a);

			// this feels wrong but leads to the right results, probably the above calculations are in the wrong color space
			maxEmissiveAmount = Mathf.LinearToGammaSpace(maxEmissiveAmount);

			output = emissiveAmount;
			intensity = maxEmissiveAmount;
		}

		private static void DecomposeScaleOffset(Vector4 input, out Vector2 scale, out Vector2 offset)
		{
			scale = new Vector2(input.x, input.y);
			offset = new Vector2(input.z, 1 - input.w - input.y);
		}

		private bool ArrayRangeEquals(object[] array, int sectionLength, int lastExportedSectionStart, int prevSectionStart, int sectionStart, int nextSectionStart)
		{
			var equals = true;
			for (int i = 0; i < sectionLength; i++)
			{
				equals &= (lastExportedSectionStart >= prevSectionStart || array[lastExportedSectionStart + i].Equals(array[sectionStart + i])) &&
				          array[prevSectionStart + i].Equals(array[sectionStart + i]) &&
				          array[sectionStart + i].Equals(array[nextSectionStart + i]);
				if (!equals) return false;
			}

			return true;
		}

		public void RemoveUnneededKeyframes(ref float[] times, ref object[] values)
		{
			if (times.Length == 1)
				return;

			removeAnimationUnneededKeyframesMarker.Begin();

			var t2 = new List<float>(times.Length);
			var v2 = new List<object>(values.Length);

			var arraySize = values.Length / times.Length;

			if (arraySize == 1)
			{
				t2.Add(times[0]);
				v2.Add(values[0]);

				int lastExportedIndex = 0;
				for (int i = 1; i < times.Length - 1; i++)
				{
					removeAnimationUnneededKeyframesCheckIdenticalMarker.Begin();
					var isIdentical = (lastExportedIndex >= i - 1 || values[lastExportedIndex].Equals(values[i])) && values[i - 1].Equals(values[i]) && values[i].Equals(values[i + 1]);
					if (!isIdentical)
					{
						lastExportedIndex = i;
						t2.Add(times[i]);
						v2.Add(values[i]);
					}
					removeAnimationUnneededKeyframesCheckIdenticalMarker.End();
				}

				var max = times.Length - 1;
				t2.Add(times[max]);
				v2.Add(values[max]);
			}
			else
			{
				var singleFrameWeights = new object[arraySize];
				Array.Copy(values, 0, singleFrameWeights, 0, arraySize);
				t2.Add(times[0]);
				v2.AddRange(singleFrameWeights);

				int lastExportedIndex = 0;
				for (int i = 1; i < times.Length - 1; i++)
				{
					removeAnimationUnneededKeyframesCheckIdenticalMarker.Begin();
					var isIdentical = ArrayRangeEquals(values, arraySize, lastExportedIndex * arraySize, (i - 1) * arraySize, i * arraySize, (i + 1) * arraySize);
					if (!isIdentical)
					{
						Array.Copy(values, (i - 1) * arraySize, singleFrameWeights, 0, arraySize);
						v2.AddRange(singleFrameWeights);
						t2.Add(times[i]);
					}

					removeAnimationUnneededKeyframesCheckIdenticalMarker.End();
				}

				var max = times.Length - 1;
				t2.Add(times[max]);
				var skipped = values.Skip((max - 1) * arraySize).ToArray();
				v2.AddRange(skipped.Take(arraySize));
			}

			times = t2.ToArray();
			values = v2.ToArray();

			removeAnimationUnneededKeyframesMarker.End();
		}
	}
}
