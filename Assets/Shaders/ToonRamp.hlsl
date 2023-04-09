void ToonShading_float(in float3 Normal, in float ToonRampSmoothness, in float4 ToonRampTinting, in float ToonRampOffset, out float3 ToonRampOutput, out float3 Direction)
{
	// set the shader graph node previews
	#ifdef SHADERGRAPH_PREVIEW
		ToonRampOutput = float3(0.5,0.5,0);
		Direction = float3(0.5,0.5,0);
	#else
		Light light = GetMainLight();
		// dot product for toonramp
		half d = dot(Normal, light.direction) * 0.5 + 0.5;
		// toonramp in a smoothstep
		half toonRamp = smoothstep(ToonRampOffset, ToonRampOffset + ToonRampSmoothness, d);
		// add in lights and extra tinting
		ToonRampOutput = light.color * (toonRamp + ToonRampTinting);
		// output direction for rimlight
		Direction = light.direction;
	#endif
}
