//
// Voronoi implementation taken from
// https://github.com/Scrawk/GPU-Voronoi-Noise
// (MIT License)
//

//1/7
#define K 0.142857142857
//3/7
#define Ko 0.428571428571

#define OCTAVES 1

vec3 fmod(vec3 x, float y) { return x - y * floor(x/y); }
vec2 fmod(vec2 x, float y) { return x - y * floor(x/y); }

// Permutation polynomial: (34x^2 + x) mod 289
vec3 Permutation(vec3 x)
{
    return mod((34.0 * x + 1.0) * x, 289.0);
}

vec2 inoise(vec3 P, float jitter)
{
    vec3 Pi = mod(floor(P), 289.0);
    vec3 Pf = fract(P);
    vec3 oi = vec3(-1.0, 0.0, 1.0);
    vec3 of = vec3(-0.5, 0.5, 1.5);
    vec3 px = Permutation(Pi.x + oi);
    vec3 py = Permutation(Pi.y + oi);
    
    vec3 p, ox, oy, oz, dx, dy, dz;
    vec2 F = vec2(1e6,1e6);
    
    for(int i = 0; i < 3; i++) {
        for(int j = 0; j < 3; j++) {
            p = Permutation(px[i] + py[j] + Pi.z + oi); // pij1, pij2, pij3
            
            ox = fract(p*K) - Ko;
            oy = mod(floor(p*K),7.0)*K - Ko;
            p = Permutation(p);
            
            oz = fract(p*K) - Ko;
            
            dx = Pf.x - of[i] + jitter*ox;
            dy = Pf.y - of[j] + jitter*oy;
            dz = Pf.z - of + jitter*oz;
            
            vec3 d = dx * dx + dy * dy + dz * dz; // dij1, dij2 and dij3, squared
            
            //Find lowest and second lowest distances
            for(int n = 0; n < 3; n++) {
                if(d[n] < F[0]) {
                    F[1] = F[0];
                    F[0] = d[n];
                } else if(d[n] < F[1]) {
                    F[1] = d[n];
                }
            }
        }
    }
    return F;
}

// fractal sum, range -1.0 - 1.0
vec2 fBm_F0(vec3 p, int octaves)
{
    //u_Frequency needs a bit of a boost for the gltf to look right
    float freq = u_Frequency * 4.;
    float amp = 0.5;
    vec2 F = inoise(p * freq, u_Jitter) * amp;
    return F;
}

