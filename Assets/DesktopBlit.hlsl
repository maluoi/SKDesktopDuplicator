//--name = app/desktop_blit
//--source = white
//--cursor = white
//--pointer = 0,0

float2 pointer;
float2 cursor_size;

Texture2D    source   : register(t0);
SamplerState source_s : register(s0);

Texture2D    cursor   : register(t1);
SamplerState cursor_s : register(s1);

cbuffer StereoKitBuffer : register(b1) {
	float4x4 sk_view       [2];
	float4x4 sk_proj       [2];
	float4x4 sk_proj_inv   [2];
	float4x4 sk_viewproj   [2];
	float4   sk_lighting_sh[9];
	float4   sk_camera_pos [2];
	float4   sk_camera_dir [2];
	float4   sk_fingertip  [2];
	float4   sk_cubemap_i;
	float    sk_time;
	uint     sk_view_count;
};
cbuffer TransformBuffer : register(b2) {
	float sk_width;
	float sk_height;
	float sk_pixel_width;
	float sk_pixel_height;
};

struct vsIn {
	float4 pos  : SV_Position;
	float3 norm : NORMAL0;
	float2 uv   : TEXCOORD0;
	float4 col  : COLOR0;
};
struct psIn {
	float4 pos : SV_POSITION;
	float2 uv  : TEXCOORD0;
	float2 cuv : TEXCOORD1;
};

psIn vs(vsIn input) {
	psIn o;
	o.pos = input.pos;
	o.uv  = input.uv;
	o.cuv = (input.uv-pointer) / cursor_size;// (pointer - input.uv) *cursor_size;
	return o;
}

float4 ps(psIn input) : SV_TARGET{
	float4 desktop_col = pow(abs(source.Sample(source_s, input.uv)), 2.2);
	float4 cursor_col  = cursor.Sample(cursor_s, input.cuv);
	float2 bounds      = input.cuv<=1  && input.cuv>=0;
	return lerp(desktop_col, cursor_col, cursor_col.a*min(bounds.x, bounds.y));
}