//@author: vux
//@help: standard constant shader
//@tags: color
//@credits: 
float4x4 tWVP: WORLDVIEWPROJECTION;

struct vsInput
{
    float4 posObject : POSITION;
};




struct gs2ps
{
    float4 pos: SV_POSITION;
    float4 col : COLOR0;
//	uint primID : SV_PrimitiveID;
};


cbuffer cbPerDraw : register(b0)
{
	float4x4 tVP : LAYERVIEWPROJECTION;
};

cbuffer cbPerObj : register( b1 )
{
	float4x4 tW : WORLD;
	float Alpha <float uimin=0.0; float uimax=1.0;> = 1; 
	float4 cAmb <bool color=true;String uiname="Color";> = { 1.0f,1.0f,1.0f,1.0f };
	float4x4 tColor <string uiname="Color Transform";>;
};


int NumberOfMeshes;
StructuredBuffer<int> ReferenceIndex;
StructuredBuffer<float4> ColorPerMesh;
StructuredBuffer<int> TrianglesPerMeshIntegral;

vsInput VS(vsInput input)
{
	//Here we just pass trough position
    return input;
}

[maxvertexcount(3)]
void GS( triangle vsInput input[3], uint primID : SV_PrimitiveID, inout TriangleStream<gs2ps> gsout )
{
	gs2ps output;
	
	//Get triangle positions
	float4 t1 = input[0].posObject.xyzw;
	float4 t2 = input[1].posObject.xyzw;
	float4 t3 = input[2].posObject.xyzw;
	
	
	//Since we assign once, triangle will have a single color
	output.col = float4(.5,.5,.5,1.0);
	for (int i = NumberOfMeshes-1; i>=0;i--){
		if (ReferenceIndex[primID]>=TrianglesPerMeshIntegral[i]){
			output.col = ColorPerMesh[i];
			break;
		}
	}
//	output.primID =;
	
	//Tranform positions and output new triangle
	output.pos = mul(t1,tWVP);	
	gsout.Append(output);
		
	output.pos = mul(t2,tWVP);	
	gsout.Append(output);
	
	output.pos = mul(t3,tWVP);	
	gsout.Append(output);
	
}


float4 PS(gs2ps input): SV_Target
{
    float4 col = input.col;
	col = mul(col, tColor);
	col.a *= Alpha;
    return col;
}


technique11 ConstantNoTexture
{
	pass P0
	{
		SetVertexShader( CompileShader( vs_4_0, VS() ) );
		SetGeometryShader( CompileShader(gs_4_0,GS()));
		SetPixelShader( CompileShader( ps_4_0, PS() ) );
	}
}





