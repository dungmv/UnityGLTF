using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AnimationMarketData", menuName = "ScriptableObjects/AnimationMarket", order = 1)]
public class AnimationMarketScriptableObject : ScriptableObject
{
    [SerializeField]
    public AnimationClip[] clips;
}
