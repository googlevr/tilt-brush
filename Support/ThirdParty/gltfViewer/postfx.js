
/**
 * Creates a canvas and fills it with a semi-transparent pattern that will be
 *     overlaid on top of a 3d model rendering.
 * @param {number} width The width (in pixels) of the web renderer.
 * @param {number} height The height (in pixels) of the web renderer.
 * @return {!Element} A canvas that contains an overlay pattern.
 */
function createGrainyVignetteCanvas(width, height) {
  const canvas = document.createElement('canvas');
  canvas.style.position = 'absolute';
  canvas.style.top = '0px';
  canvas.style.left = '0px';
  canvas.style.pointerEvents = 'none';
  canvas.width = width;
  canvas.height = height;

  const context = canvas.getContext('2d');
  const imageData = context.createImageData(width, height);
  const data = imageData.data;

  const cx = width / 2;
  const cy = height / 2;
  const size = Math.hypot(width, height);

  // Iterate through all pixels on the canvas and add some amount of
  // transparency to them, depending on how far they are from the center (to
  // create a vignette pattern).
  for (let x = 0; x < width; x++) {
    for (let y = 0; y < height; y++) {
      const bufferIndex = (x + y * width) * 4;
      const distanceFromCenter = Math.hypot(x - cx, y - cy) / size;

      // Set rgb to black.
      data[bufferIndex] = data[bufferIndex + 1] = data[bufferIndex + 2] = 0;

      // Set alpha channel to match vignette pattern, with some random noise.
      data[bufferIndex + 3] =
          100 * Math.pow(distanceFromCenter, 2.0) + Math.random() * 10;
    }
  }

  context.putImageData(imageData, 0, 0);

  return canvas;
};

var overlayShader = {
  uniforms: {
    'tDiffuse': {type: 't', value: null},
    'noiseStrength': {type: 'f', value: 4.0},
    'vignetteStrength': {type: 'f', value: 0.5},
  },

  vertexShader: `
    varying vec2 vUv;

    void main() {
      vUv = uv;
      gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
    }
  `,

  fragmentShader: `
    uniform sampler2D tDiffuse;
    uniform float noiseStrength;
    uniform float vignetteStrength;
    varying vec2 vUv;

    void main() {
      vec4 color = texture2D(tDiffuse, vUv);

      float xy = (vUv.x + 4.0) * (vUv.y + 4.0) * 10.0;
      vec4 noise =
        vec4(mod((mod(xy, 13.0) + 1.0) * (mod(xy, 123.0) + 1.0), 0.01) - 0.005);

      float distanceFromCenter = length(vUv - 0.5);

      gl_FragColor = color
        * (1.0 - noise * noiseStrength)
        * (1.0 - distanceFromCenter * distanceFromCenter * vignetteStrength);
    }
  `,
};

  var blurVignetteLensShader = {
    uniforms: {
      'tDiffuse': {type: 't', value: null},
      'noiseStrength': {type: 'f', value: .06},
      'vignetteStrength': {type: 'f', value: 0.5},
      'resolution': {type: "v2", value: new THREE.Vector2(0, 0)},
      'blurTexture1': {value: null},
      'blurTexture2': {value: null},
      'blurTexture3': {value: null},
      'u_time': {type: 'v4', value: new THREE.Vector4(0, 0, 0, 0)},
    },

    vertexShader: `
      varying vec2 vUv;

  void main() {
    vUv = uv;
    gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
  }
    `,

  fragmentShader: `
  uniform sampler2D tDiffuse;
  uniform float noiseStrength;
  uniform float vignetteStrength;
  uniform vec2 resolution;
  varying vec2 vUv;
  uniform sampler2D blurTexture1;
  uniform sampler2D blurTexture2;
  uniform sampler2D blurTexture3;
  uniform vec4 u_time;

  #define pi 3.141592653589793238462643383279

  vec2 toPolar(vec2 uv, vec2 center){
    vec2 r = uv - center;
    return vec2(atan(r.x,r.y), length(r));
  }

  vec2 uvLens(vec2 uv, float radius, float refractivity){
    vec2 polar = toPolar(uv, vec2(.5, .5));
    float cone = clamp(1. - polar.y / radius, 0., 1.);
    float halfsphere = sqrt(1. - pow(cone - 1., 2.));
    float w = atan(1.-cone, halfsphere);
    float refractW = w-asin(sin(w) / refractivity);
    float refractD = 1.-cone - sin(refractW) * halfsphere / cos(refractW);
    return vec2(.5, .5) + vec2(sin(polar.x), cos(polar.x)) * refractD * radius;
  }

  float roundRect(vec2 p, vec2 ab, float r) {
    return length(max(abs(p) - ab, 0.0)) - r;
  }
  void main() {

    vec2 uv = vUv; //fragCoord.xy / iResolution.xy;
    vec2 aspect = vec2(1., min(resolution.y, resolution.x) / max(resolution.x, resolution.y));
    
    float curve[7];
    curve[0] = 0.020;
    curve[1] = 0.035;
    curve[2] = 0.109;
    curve[3] = 0.172;
    curve[4] = 0.109;
    curve[5] = 0.035;
    curve[6] = 0.020;
    
    vec3 color = vec3(0);
    vec2 duv = 1.0 / resolution;
    
    vec2 adj = (uv * 2. - 1.) * aspect;
    vec2 adjCh = clamp(adj * .006, -duv*2., duv*2.); //uv * 2. - 1.;

    float xy = (vUv.x + 4.0 + u_time.z) * (vUv.y + 4.0 + u_time.w) * 10.0;
    vec4 noise =
        2.* clamp(200. * vec4(mod((mod(xy, 13.0) + 1.0) * (mod(xy, 123.0) + 1.0), .01) - 0.005), 0., 1.) - 1.;

    float r = length(vec2(adj.x, adj.y));
    r = r*r;
    
    //float r2 = length(vec2(min(uv.x, abs(1. - uv.x)), min(uv.y, abs(1. - uv.y))));
    //r2 = max(r2 * 1.5, .01);
    float r2 = max(1. - length(vec2(adj.x, adj.y))*1., .01);
    r2 = roundRect((uv * 2. - 1.) * aspect, vec2(.5, .3) * aspect, .2);
    r2 = 1. - r2 * 2.;
    //r2 = pow(r2, .3);
    //r2 *= r2;

    //uv = uvLens(uv, 0.95, 1.1);
    vec2 uv2 = uvLens(uv, 0.95, 1.03);
    /*
    for (float i = 0.0; i < 7.; i++) {
        color.rb += texture2D(tDiffuse, uv + vec2(0., duv.y * i * 1.5), 10.0).rb * curve[int(i)];
        color.g += texture2D(tDiffuse, uv2 + vec2(0., duv.y * i * 1.5 + adjCh.y)).g * curve[int(i)];
    }
    for (float i = 0.0; i < 7.; i++) {
        color.rb += texture2D(tDiffuse, uv + vec2(duv.x * i * 1.5, 0.)).rb * curve[int(i)];
        color.g += texture2D(tDiffuse, uv2 + vec2(duv.x * i * 1.5 + adjCh.x, 0.)).g * curve[int(i)];
    }*/
    //color = texture2D(blurTexture1, uv).rgb;
    //color += texture2D(blurTexture2, uv).rgb;
    color += 100. * texture2D(blurTexture3, uv).rgb;

    //color.r += 100. * texture2D(blurTexture3, uv + adjCh).r;
    //color.g += 100. * texture2D(blurTexture3, uv + vec2(-adjCh.x, adjCh.y)).g;
    //color.b += 100. * texture2D(blurTexture3, uv + adjCh).b;

    color /= 100.;
    color = clamp(color, vec3(0.), vec3(3.));


    //color += 3. * texture2D(blurTexture3, uv).rgb;
    //color.g += texture2D(blurTexture3, uv + adjCh).g;
    //color.b += texture2D(blurTexture3, uv + adjCh.yx).b;
    
    vec3 colorAbbr = vec3(0);
    colorAbbr.g += texture2D(tDiffuse, uv + vec2(-adjCh.x, adjCh.y)).g; //vec2(0., adjCh.x + adjCh.y)).g;
    colorAbbr.b += texture2D(tDiffuse, uv + adjCh).b;
    colorAbbr.r += texture2D(tDiffuse, uv + adjCh).r; //vec2(-adjCh.x, adjCh.y)).r;
    color = mix(color, colorAbbr, clamp(1. - r*r, 0., 1.));
    //color += 
    //#endif

    color += mix(vec3(.0), noise.rgb * noiseStrength, clamp(r, 0.3, 1.));

    colorAbbr = texture2D(tDiffuse, uv).rgb;


    //color *= r2;
    //gl_FragColor.rgb = mix(color, colorAbbr, clamp(r, 0., 1.));
    gl_FragColor.rgb = mix(color, colorAbbr, clamp(1. - pow(r, 1.), 0., 1.)); //clamp(1. - r, 0., 1.));
    gl_FragColor.rgb *= (1. - r * .5);
    //gl_FragColor.rgb *= clamp(1. - (1. - r2) * .3, 0., 1.) * (1. - r);
    //gl_FragColor.rgb = vec3(r2 );
    //gl_FragColor.rgb = noise.rgb;
    //gl_FragColor.rgb = texture2D(tDiffuse, uv).rgb;
    gl_FragColor.a = 1.0;
  }
    `,
  };


/**
 * A rendering pass which should replicate Tilt Brush's bloom effect using a
 * series of screen-space shaders.
 * @extends {THREE.Pass}
 */
class BloomPass {
  /**
   * @param {!THREE.WebGLRenderer} renderer
   * @param {?Object=} resolution
   * @param {number=} intensity
   * @param {number=} blurSize
   */
  constructor(renderer, resolution, intensity, blurSize) {
    /**
     * If set to true, the pass is processed by the composer.
     * @type {boolean}
     */
    this.enabled = true;

    /**
     * If set to true, the pass indicates to swap read and write buffer after
     * rendering.
     * @type {boolean}
     */
    this.needsSwap = false;

    /**
     * If set to true, the pass clears its buffer before rendering.
     * @type {boolean}
     */
    this.clear = false;

    /**
     * If set to true, the result of the pass is rendered to screen.
     * @type {boolean}
     */
    this.renderToScreen = false;

    /** @private @type {number} */
    this.intensity_ = (intensity !== undefined) ? intensity : 0.05;

    /** @private @type {number} */
    this.blurSize_ = (blurSize !== undefined) ? blurSize : 4.0;

    /** @private @type {!THREE.Vector2} */
    this.resolution_ = (resolution !== undefined) ?
        new THREE.Vector2(resolution.x, resolution.y) :
        new THREE.Vector2(256, 256);

    /** @private @type {number} */
    this.maxVertical_ = 1080.0;
    if (resolution.y > this.maxVertical_) {
      const scale = this.maxVertical_ / resolution.y;
      this.resolution_.x = Math.floor(resolution.x * scale);
      this.resolution_.y = Math.floor(resolution.y * scale);
    }

    const pars = {
      wrapS: THREE.ClampToEdgeWrapping,
      wrapT: THREE.ClampToEdgeWrapping,
      minFilter: THREE.LinearFilter,
      magFilter: THREE.LinearFilter,
      format: THREE.RGBAFormat,
      type: THREE.HalfFloatType,
    };

    //if (!renderer.extensions.get('EXT_color_buffer_half_float')) {
    if (!renderer.extensions.get('OES_texture_half_float')) {
      pars['type'] = THREE.UnsignedByteType;
      pars['type'] = THREE.FloatType;
    }

    if (!renderer.extensions.get('OES_texture_half_float_linear')) {
      pars['minFilter'] = THREE.NearestFilter;
      pars['magFilter'] = THREE.NearestFilter;
    }

    /** @private @type {!Array} */
    this.renderTargets_ = [];

    /** @private @type {number} */
    this.passCount_ = 3;

    let resx = Math.floor(this.resolution_.x / 2);
    let resy = Math.floor(this.resolution_.y / 2);
    for (let i = 0; i < this.passCount_; i++) {
      const renderTarget0 = new THREE.WebGLRenderTarget(resx, resy, pars);
      const renderTarget1 = new THREE.WebGLRenderTarget(resx, resy, pars);
      renderTarget0.texture.generateMipmaps = false;
      renderTarget1.texture.generateMipmaps = false;

      this.renderTargets_.push(renderTarget0);
      this.renderTargets_.push(renderTarget1);

      resx = Math.floor(resx / 2);
      resy = Math.floor(resy / 2);
    }

    // Now validate the render targets...
    var gl = renderer.context;
    if (gl.checkFramebufferStatus(gl.FRAMEBUFFER) !== gl.FRAMEBUFFER_COMPLETE) {
      // Bloom is not supported on this hardware.
      // TODO(evanmoore): low quality bloom for hardware/drivers that do not
      // support this?
      this.enabled = false;
      return;
    }

    /** @private @type {!THREE.Color} */
    this.oldClearColor_ = new THREE.Color();

    /** @private @type {number} */
    this.oldClearAlpha_ = 1;

    /** @private @type {!THREE.OrthographicCamera} */
    this.camera_ = new THREE.OrthographicCamera(-1, 1, 1, -1, 0, 1);
    /** @private @type {!THREE.Scene} */
    this.scene_ = new THREE.Scene();

    /** @private @type {!THREE.Mesh} */
    this.quad_ = new THREE.Mesh(new THREE.PlaneBufferGeometry(2, 2));
    this.quad_.frustumCulled = false;  // Avoid getting clipped
    this.scene_.add(this.quad_);

    /** @private @type {!THREE.ShaderMaterial} */
    this.materialDownsample_ = new THREE.ShaderMaterial({
      uniforms: {
        'baseTexture': {value: null},
        'texelSize': {value: new THREE.Vector2(0.0, 0.0)}
      },

      vertexShader: `
        varying vec2 uv20;
        varying vec2 uv21;
        varying vec2 uv22;
        varying vec2 uv23;

        uniform vec2 texelSize;

        void main() {
          uv20 = uv + texelSize;
          uv21 = uv + texelSize * vec2(-0.5, -0.5);
          uv22 = uv + texelSize * vec2( 0.5, -0.5);
          uv23 = uv + texelSize * vec2(-0.5,  0.5);
          gl_Position =
            projectionMatrix * modelViewMatrix * vec4(position, 1.0);
        }
      `,

      fragmentShader: `
        varying vec2 uv20;
        varying vec2 uv21;
        varying vec2 uv22;
        varying vec2 uv23;

        uniform sampler2D baseTexture;

        void main() {
          vec4 color;
          color  = texture2D(baseTexture, uv20);
          color += texture2D(baseTexture, uv21);
          color += texture2D(baseTexture, uv22);
          color += texture2D(baseTexture, uv23);
          gl_FragColor = max(color * 0.25, vec4(0.0));
        }
      `
    });

    /** @private @type {!THREE.ShaderMaterial} */
    this.materialBlur_ = new THREE.ShaderMaterial({
      uniforms: {
        'baseTexture': {value: null},
        'texelSize': {value: new THREE.Vector2(0.0, 0.0)},
        'blurSize': {value: 4.0},
        'curve':
            {value: [0.0205, 0.0855, 0.232, 0.324, 0.232, 0.0855, 0.0205, 0.0]},
      },

      vertexShader: `
        varying vec2 vUv;
        varying vec4 vOffset;

        uniform vec2 texelSize;
        uniform float blurSize;

        void main() {
          vOffset = vec4(texelSize.xy * blurSize, 1, 1);
          vUv = uv - vOffset.xy * 3.0;
          gl_Position =
            projectionMatrix * modelViewMatrix * vec4(position, 1.0);
        }
      `,

      fragmentShader: `
        varying vec2 vUv;
        varying vec4 vOffset;

        uniform sampler2D baseTexture;
        uniform float curve[8];

        void main() {
          vec2 netFilterWidth = vOffset.xy;
          vec2 coords = vUv;
          vec4 color = vec4(0.0);
          for (int l = 0; l < 7; l++) {
            vec4 tap = texture2D(baseTexture, coords);
            color += tap * vec4(curve[l]);
            coords += netFilterWidth;
          }
          gl_FragColor = color;
        }
      `
    });

    /** @private @type {!THREE.ShaderMaterial} */
    this.materialBloom_ = new THREE.ShaderMaterial({
      uniforms: {
        'blurTexture1': {value: null},
        'blurTexture2': {value: null},
        'blurTexture3': {value: null},
        'bloomIntensity': {value: 0.05},
      },

      transparent: true,

      blending: THREE['NormalBlending'],

      vertexShader: `
        varying vec2 vUv;
        varying vec4 vOffset;

        void main() {
          vUv = uv;
          gl_Position =
            projectionMatrix * modelViewMatrix * vec4(position, 1.0);
        }
      `,

      fragmentShader: `
        varying vec2 vUv;

        uniform sampler2D blurTexture1;
        uniform sampler2D blurTexture2;
        uniform sampler2D blurTexture3;
        uniform float bloomIntensity;

        void main() {
          vec2 coord = vUv;
          highp vec3 b0 = texture2D(blurTexture1, coord).rgb;
          highp vec3 b1 = texture2D(blurTexture2, coord).rgb;
          highp vec3 b2 = texture2D(blurTexture3, coord).rgb;
          highp vec3 bloom = b0 * 0.5 + b1 * 0.6 + b2 * 0.6;
          bloom /= 1.2;

          vec4 color = vec4(bloom.rgb, bloomIntensity);
          gl_FragColor = color;
        }
      `
    });
  }

  /**
   * Clean up any render targets that this class created.
   */
  dispose() {
    this.renderTargets_.forEach(renderTarget => {
      renderTarget.dispose();
    });
  }

  /**
   * @param {number} width
   * @param {number} height
   */
  setSize(width, height) {
    if (height > this.maxVertical_) {
      const scale = this.maxVertical_ / height;
      this.resolution_.x = Math.floor(width * scale);
      this.resolution_.y = Math.floor(height * scale);
    } else {
      this.resolution_.x = width;
      this.resolution_.y = height;
    }

    let resx = Math.round(this.resolution_.x / 2);
    let resy = Math.round(this.resolution_.y / 2);

    for (let i = 0; i < this.passCount_; i++) {
      this.renderTargets_[i * 2 + 0].setSize(resx, resy);
      this.renderTargets_[i * 2 + 1].setSize(resx, resy);

      resx = Math.floor(resx / 2);
      resy = Math.floor(resy / 2);
    }
  }

  /**
   * @param {!THREE.WebGLRenderer} renderer
   * @param {!THREE.WebGLRenderTarget} writeBuffer
   * @param {!THREE.WebGLRenderTarget} readBuffer
   * @param {number} delta
   * @param {boolean} maskActive
   */
  render(renderer, writeBuffer, readBuffer, delta, maskActive) {
    // Store state and setup
    this.oldClearColor_.copy(renderer.getClearColor());
    this.oldClearAlpha_ = renderer.getClearAlpha();
    const oldAutoClear = renderer.autoClear;
    renderer.autoClear = false;
    renderer.setClearColor(new THREE.Color(0, 0, 0), 0);

    if (maskActive) {
      renderer.context.disable(renderer.context.STENCIL_TEST);
    }
    let iterations = 1;  // number of blur iterations per pass.
    let src = readBuffer.texture;
    const res = new THREE.Vector2(this.resolution_.x, this.resolution_.y);

    // Downsample and blur.
    for (let i = 0; i < this.passCount_; i++) {
      const rt0 = this.renderTargets_[i * 2];
      const rt1 = this.renderTargets_[i * 2 + 1];
      this.downsample_(renderer, src, rt0, res);

      // fixed sized blur
      const spread = 1.0;

      // only 1 blur iteration on the first pass, then do 2 on additional passes
      // (when the pixel count is 1/4 and 1/16)
      if (i == 0) {
        iterations = 1;
      } else {
        iterations = 2;
      }

      for (let j = 0; j < iterations; j++) {
        const passBlurSize = (this.blurSize_ * 0.5 + j) * spread;
        // Vertical & Horizontal Blur passes.
        this.applyBlur_(renderer, rt0.texture, rt1, res, passBlurSize, true);
        this.applyBlur_(renderer, rt1.texture, rt0, res, passBlurSize, false);
      }

      src = rt0.texture;
      res.x = Math.floor(res.x / 2);
      res.y = Math.floor(res.y / 2);
    }
    // Finally apply the bloom and output to the screen.
    this.applyBloom_(renderer, readBuffer, this.intensity_);

    // Cleanup
    if (maskActive) {
      renderer.context.enable(renderer.context.STENCIL_TEST);
    }
    renderer.setClearColor(this.oldClearColor_, this.oldClearAlpha_);
    renderer.autoClear = oldAutoClear;
  }

  /**
   * @param {!THREE.WebGLRenderer} renderer
   * @param {?THREE.Texture} src
   * @param {!THREE.WebGLRenderTarget} dst
   * @param {!THREE.Vector2} srcRes
   * @private
   */
  downsample_(renderer, src, dst, srcRes) {
    this.materialDownsample_.uniforms['baseTexture'].value = src;
    this.materialDownsample_.uniforms['texelSize'].value.x = 1.0 / srcRes.x;
    this.materialDownsample_.uniforms['texelSize'].value.y = 1.0 / srcRes.y;
    this.quad_.material = this.materialDownsample_;
    renderer.render(this.scene_, this.camera_, dst, true);
  }

  /**
   * @param {!THREE.WebGLRenderer} renderer
   * @param {?THREE.Texture} src
   * @param {!THREE.WebGLRenderTarget} dst
   * @param {!THREE.Vector2} srcRes
   * @param {number} blurSize
   * @param {boolean} verticalBlur
   * @private
   */
  applyBlur_(renderer, src, dst, srcRes, blurSize, verticalBlur) {
    this.materialBlur_.uniforms['baseTexture'].value = src;
    this.materialBlur_.uniforms['texelSize'].value.x =
        verticalBlur ? 0.0 : (0.5 / srcRes.x);
    this.materialBlur_.uniforms['texelSize'].value.y =
        verticalBlur ? (0.5 / srcRes.y) : 0.0;
    this.materialBlur_.uniforms['blurSize'].value = blurSize;
    this.quad_.material = this.materialBlur_;
    renderer.render(this.scene_, this.camera_, dst, true);
  }

  /**
   * @param {!THREE.WebGLRenderer} renderer
   * @param {!THREE.WebGLRenderTarget} dst
   * @param {number} intensity
   * @private
   */
  applyBloom_(renderer, dst, intensity) {
    this.materialBloom_.uniforms['blurTexture1'].value =
        this.renderTargets_[0].texture;
    this.materialBloom_.uniforms['blurTexture2'].value =
        this.renderTargets_[2].texture;
    this.materialBloom_.uniforms['blurTexture3'].value =
        this.renderTargets_[4].texture;
    this.materialBloom_.uniforms['bloomIntensity'].value = intensity;
    this.quad_.material = this.materialBloom_;
    renderer.render(this.scene_, this.camera_, dst, false);
  }
}

/**
 * @extends {THREE.Pass}
 */
class SuperSamplePass {
  /**
   * @param {!THREE.WebGLRenderer} renderer
   * @param {!Object} resolution
   * @param {boolean} useFloatBuffers
   */
  constructor(renderer, resolution, useFloatBuffers) {
    /**
     * If set to true, the pass is processed by the composer.
     * @type {boolean}
     */
    this.enabled = true;

    /**
     * If set to true, the pass indicates to swap read and write buffer after
     * rendering.
     * @type {boolean}
     */
    this.needsSwap = false;

    /**
     * If set to true, the pass clears its buffer before rendering.
     * @type {boolean}
     */
    this.clear = false;

    /**
     * If set to true, the result of the pass is rendered to screen.
     * @type {boolean}
     */
    this.renderToScreen = false;

    /** @type {!THREE.Vector2} */
    this.resolution = (resolution !== undefined) ?
        new THREE.Vector2(resolution.x, resolution.y) :
        new THREE.Vector2(256, 256);

    const pars = {
      wrapS: THREE.ClampToEdgeWrapping,
      wrapT: THREE.ClampToEdgeWrapping,
      minFilter: THREE.LinearFilter,
      magFilter: THREE.LinearFilter,
      format: THREE.RGBAFormat,
      type: THREE.HalfFloatType
    };

        // !renderer.extensions.get('EXT_color_buffer_half_float')) {
    if (!useFloatBuffers ||
        !renderer.extensions.get('OES_texture_half_float')) {
      pars['type'] = THREE.UnsignedByteType;
    }

    if (!renderer.extensions.get('OES_texture_half_float_linear') &&
        pars.type !== THREE.UnsignedByteType) {
      pars['minFilter'] = THREE.NearestFilter;
      pars['magFilter'] = THREE.NearestFilter;
    }

    /** @type {!Array} */
    this.renderTargets = [];
    /** @type {!Array} */
    this.halton2 = [];
    /** @type {!Array} */
    this.halton3 = [];
    /** @type {number} */
    this.jitter = 0;
    /** @type {number} */
    this.prevIndex = 0;
    /** @type {number} */
    this.nextIndex = 1;
    /** @type {boolean} */
    this.enablePass = false;
    /** @type {boolean} */
    this.resetAccum = true;

    this.buildHaltonSequence(this.halton2, 2, 64);
    this.buildHaltonSequence(this.halton3, 3, 64);

    this.renderTargets[0] =
        new THREE.WebGLRenderTarget(this.resolution.x, this.resolution.y, pars);
    this.renderTargets[1] =
        new THREE.WebGLRenderTarget(this.resolution.x, this.resolution.y, pars);

    /** @type {!THREE.Color} */
    this.oldClearColor = new THREE.Color();
    /** @type {number} */
    this.oldClearAlpha = 1;

    /** @type {!THREE.OrthographicCamera} */
    this.camera = new THREE.OrthographicCamera(-1, 1, 1, -1, 0, 1);
    /** @type {!THREE.Scene} */
    this.scene = new THREE.Scene();

    /** @type {!THREE.Mesh} */
    this.quad = new THREE.Mesh(new THREE.PlaneBufferGeometry(2, 2));
    this.quad.frustumCulled = false;  // Avoid getting clipped
    this.scene.add(this.quad);

    /** @type {number} */
    this.sampleCount = 64;
    /** @type {number} */
    this.blend = 1.0 / this.sampleCount;
    /** @type {number} */
    this.footprint = 1.5;

    /** @type {!THREE.ShaderMaterial} */
    this.materialAccumulate = new THREE.ShaderMaterial({
      uniforms: {
        'prevFrame': {value: null},
        'curFrame': {value: null},
        'blend': {value: 0.05},
      },

      vertexShader: `
        varying vec2 vUv;

        void main() {
          vUv = uv;
          gl_Position =
            projectionMatrix * modelViewMatrix * vec4(position, 1.0);
        }
      `,

      fragmentShader: `
        varying vec2 vUv;

        uniform sampler2D prevFrame;
        uniform sampler2D curFrame;
        uniform float blend;

        void main() {
          vec4 prevColor =
            clamp(texture2D(prevFrame, vUv), vec4(0.0), vec4(1.0));
          vec4 curColor = clamp(texture2D(curFrame, vUv), vec4(0.0), vec4(1.0));
          gl_FragColor = prevColor * (1.0 - blend) + curColor * blend;
        }
      `
    });

    this.materialCopy = new THREE.ShaderMaterial({
      uniforms: {
        'tDiffuse': {value: null},
      },

      vertexShader: `
        varying vec2 vUv;

        void main() {
          vUv = uv;
          gl_Position =
            projectionMatrix * modelViewMatrix * vec4(position, 1.0);
        }
      `,

      fragmentShader: `
        varying vec2 vUv;

        uniform sampler2D tDiffuse;

        void main() {
          gl_FragColor = texture2D(tDiffuse, vUv);
        }
      `
    });
  }

  /**
   * Clean up any render targets that this class created.
   */
  dispose() {
    this.renderTargets.forEach(renderTarget => renderTarget.dispose());
  }

  /**
   * @param {number} width
   * @param {number} height
   */
  setSize(width, height) {
    this.resolution.x = width;
    this.resolution.y = height;

    this.renderTargets[0].setSize(width, height);
    this.renderTargets[1].setSize(width, height);
  }

  /**
   * @param {!THREE.WebGLRenderer} renderer
   * @param {!THREE.WebGLRenderTarget} writeBuffer
   * @param {!THREE.WebGLRenderTarget} readBuffer
   * @param {number} delta
   * @param {boolean} maskActive
   */
  render(renderer, writeBuffer, readBuffer, delta, maskActive) {
    if (!this.enablePass) {
      return;
    }

    // Store state and setup
    this.oldClearColor.copy(renderer.getClearColor());
    this.oldClearAlpha = renderer.getClearAlpha();
    const oldAutoClear = renderer.autoClear;
    renderer.autoClear = false;
    renderer.setClearColor(new THREE.Color(0, 0, 0), 0);

    if (maskActive) {
      renderer.context.disable(renderer.context.STENCIL_TEST);
    }

    const src = readBuffer.texture;

    // This will be true if there is no valid previous frame data.
    if (this.resetAccum) {
      // Copy next to the output.
      this.materialCopy.uniforms['tDiffuse'].value = readBuffer.texture;
      this.quad.material = this.materialCopy;
      renderer.render(
          this.scene, this.camera, this.renderTargets[this.prevIndex], true);
    } else {
      // Pass in the previous frame and current frame -> next frame
      this.materialAccumulate.uniforms['prevFrame'].value =
          this.renderTargets[this.prevIndex].texture;
      this.materialAccumulate.uniforms['curFrame'].value = src;
      this.materialAccumulate.uniforms['blend'].value = this.blend;
      this.quad.material = this.materialAccumulate;
      renderer.render(
          this.scene, this.camera, this.renderTargets[this.nextIndex], true);

      // Copy next to the output.
      this.materialCopy.uniforms['tDiffuse'].value =
          this.renderTargets[this.nextIndex].texture;
      this.quad.material = this.materialCopy;
      renderer.render(this.scene, this.camera, readBuffer, true);

      // Swap so next becomes previous
      this.prevIndex = 1 - this.prevIndex;
      this.nextIndex = 1 - this.nextIndex;
    }

    // Cleanup
    if (maskActive) {
      renderer.context.enable(renderer.context.STENCIL_TEST);
    }
    renderer.setClearColor(this.oldClearColor, this.oldClearAlpha);
    renderer.autoClear = oldAutoClear;
    this.resetAccum = false;
  }

  /**
   * @param {!THREE.PerspectiveCamera} camera
   */
  adjustProjectionMatrix(camera) {
    if (!this.enablePass) {
      return;
    }

    // Apply the sub-pixel offset to the camera.
    const x = (this.halton2[this.jitter] - 0.5) * this.footprint;
    const y = (this.halton3[this.jitter] - 0.5) * this.footprint;
    camera.setViewOffset(
        this.resolution.x, this.resolution.y, x, y, this.resolution.x,
        this.resolution.y);
    camera.updateProjectionMatrix();
    this.jitter = (this.jitter + 1) % this.sampleCount;
  }

  /**
   * @param {boolean} enable
   */
  enableSSAA(enable) {
    this.enablePass = enable;
    this.resetAccum = true;
  }

  /**
   * @param {number} count
   */
  setSampleCount(count) {
    if (count != this.sampleCount) {
      if (count > 64) {
        count = 64;
      }
      this.sampleCount = count;
      this.blend = 1.0 / this.sampleCount;
      this.resetAccum = true;
    }
  }

  /**
   * @param {number} sizeInPixels
   */
  setFootprint(sizeInPixels) {
    if (sizeInPixels != this.footprint) {
      this.footprint = sizeInPixels;
      this.resetAccum = true;
    }
  }

  /**
   * @param {number} factor
   */
  overrideAccumSpeed(factor) {
    const newBlend = factor / this.sampleCount;
    if (newBlend != this.blend) {
      this.blend = newBlend;
      this.resetAccum = true;
    }
  }

  /**
   * @param {number} i
   * @param {number} b
   * @return {number}
   */
  halton(i, b) {
    let f = 1.0;
    let r = 0.0;
    let idx = i + 1;

    while (idx > 0) {
      f = f / b;
      r = r + f * (idx % b);
      idx = Math.floor(idx / b);
    }

    return r;
  }

  /**
   * @param {!Array} seq
   * @param {number} b
   * @param {number} count
   */
  buildHaltonSequence(seq, b, count) {
    for (let i = 0; i < count; i++) {
      seq[i] = this.halton(i, b);
    }
  }
}
