> ⚠️ **STATUS — PARTIALLY SUPERSEDED (read this first).** This doc originally recommended **Option A** (show the *real* card via depth-occlusion / "hole-punch") as primary. The project has since chosen **Option B — card replacement** (render a tracked *virtual* card locked onto the real one) as the **canonical primary approach on all platforms**, per the Option B decision and `master-roadmap.md` (M6). Consequences: (1) Option B needs only pose tracking, not per-pixel depth, so it works on Vision Pro/Android XR too and enables live/animated card faces; (2) **hand occlusion is still required** in Option B (to keep fingers in front of the virtual card) — the hand-occlusion engineering in this doc remains fully valid and needed; (3) the Option-A occlusion analysis here is retained as the **fallback / optional MR-authenticity mode**, not the default. See the Card Visibility entry in `00-INDEX.md` for the canonical summary.

---

# Physical Card Visibility in VR/MR — Engineering Design Review

*An engineering answer to: "how do we actually keep the real physical cards visible while a virtual world renders behind them?" Grounded in Meta's Depth API / Passthrough documentation, the depth-buffer occluder technique, and cross-platform depth APIs. This is a design review, not a pitch — it names the mechanism, the failure modes, and the exact fallback stack.*

---

## 0. Reframing the problem correctly

"Not painting over the cards" is not a thing you *do* — it's an emergent result of **compositing order + a depth test.** The real questions are:

1. How is passthrough (real video) composited with the virtual world? → **layer compositing** (§1)
2. How does the GPU decide, per pixel, whether the real card or the virtual world wins? → **the depth test** (§2)
3. Where does the per-pixel depth of the card come from? → **environment depth vs. tracked-occluder mesh** (§3)
4. Why is the card harder than the hand? → **first-party tracked mesh vs. none** (§4)
5. What's the shippable stack and its failure handling? → **§6–§9**

## 1. How passthrough composites (the foundation)

On Quest 3, passthrough is a **compositor layer**, not something your app renders into a texture. Per Meta's docs, the passthrough layer is placed either **behind** the app layer (Passthrough AR / "underlay" mode) or **in front** (Overlay). For our game the world is virtual, so the natural mode is **underlay: passthrough sits behind, the virtual world renders on top.**

By default that means the virtual world *completely covers* passthrough — you'd see no cards. **Occlusion = selectively punching holes in the virtual layer so the passthrough underlay shows through where real objects are closer.** That's the entire trick. "Keeping the card visible" literally means "ensuring the virtual world's pixels are *discarded* wherever the card is, so the passthrough layer behind them is revealed."

## 2. The depth-test mechanism (the exact GPU pipeline)

Meta documents two ways to punch the hole; both are pure depth-buffer manipulation:

**(A) Explicit depth-only occluder (the classic "hole-punch"), verbatim from Meta:**
> "Render the hands or an occluder with a material that only writes to the depth buffer, before other objects (render queue < 2000). Subsequent virtual objects or materials fail the depth test and will not render in the area of the occluder, giving the illusion of being behind the occluder."

So: render an invisible mesh at the real object's position that writes **depth only** (`ColorMask 0`, `ZWrite On`) early in the frame. Every virtual pixel behind it fails `ZTest` and is discarded → passthrough shows through. This is a *known-geometry* technique: it needs the object's mesh and pose.

**(B) Per-pixel environment depth (Depth API):** the system provides a **real-time, per-eye, per-frame depth map of everything the cameras see.** Occlusion shaders sample this depth texture and discard virtual fragments that are farther than the sampled real depth. This is *geometry-agnostic* — it occludes hands, arms, table, people, pets, **and a held card**, because the card is just real geometry in the depth map. Meta ships this as `OcclusionsEnabled` in the Depth API with **hard** (cheap, jagged) and **soft** (dilated/blended edges, costlier) modes; the `SelectivePassthrough` sample provides the soft/translucent shader.

Both write into the same depth buffer the virtual scene tests against. They are **composable** — you can use environment depth for the general scene *and* explicit occluders for specific known objects in the same frame (alternating render queues + `ZTest`/`ZWrite`). This composability is the key to our recommendation.

## 3. Where the card's depth comes from — the core decision

| Source of card depth | Mechanism | Card handled? | Weakness on cards |
|---|---|---|---|
| **Depth API environment depth** | per-pixel stereo depth of whole scene | ✅ automatically (card = geometry) | thin object; **noisy/rounded at card edges**; unreliable <0.2 m; struggles on glossy/foil/low-texture surfaces (stereo matching needs texture) |
| **Fiducial-tracked occluder quad** | we know exact card pose from the fiducial → render a card-shaped depth-only quad (technique A) | ✅ precisely | needs the fiducial locked; a flat quad ignores finger-over-card |
| **Hand mesh occluder** | first-party tracked hand mesh (technique A) | ❌ (hands only) | — |

**Answer to your direct question — "does Depth API solve cards automatically because the card is geometry in the depth map, or is more processing needed?"** Both are true: Depth API *does* occlude the card for free, **but** the card is precisely the worst case for stereo environment depth — thin, often glossy/foil, low-texture, held near the face — so its *edges* will shimmer, round off, or drop out, especially in motion. For a UI object the player stares at and manipulates, that quality gap is unacceptable on its own. So: environment depth is the *base*, and the fiducial-tracked occluder is the *reinforcement* that guarantees a crisp, stable card silhouette exactly where the depth map is weakest.

## 4. Why hands "just work" but cards don't

Because **hands have a first-party tracked, articulated mesh and cards don't.** The OS ships a hand-tracking model that yields a full 3D hand mesh every frame → you can render it as a depth-only occluder (technique A) *and* Depth API covers hands per-pixel. Two independent, well-tuned solutions, both maintained by Meta.

A card has **no first-party tracker.** Generic environment depth is its only free solution, and that's the noisy path. Our advantage: the **fiducial frame gives us a card tracker Meta doesn't ship** — pose + orientation every frame → we synthesize the "missing" tracked mesh (a quad matching the card's dimensions) and get hand-quality occlusion for the card. This is the single most important architectural insight: *the fiducial we already need for identity also gives us the pose we need for clean occlusion.* Identity and occlusion are different jobs (a fiducial doesn't occlude, a depth map doesn't identify) but they share the same pose estimate.

## 5. Behavior under real card handling (rotate / flip / fan / stack / finger-cover)

| Action | Environment-depth layer | Fiducial-occluder layer | Net result |
|---|---|---|---|
| Rotate / tilt | follows (per-pixel) | quad follows fiducial pose exactly | clean |
| Flip face-away | follows | fiducial ID lost on back → **fall back to env depth only** (card still occludes, just no crisp quad); or print a back-side fiducial | acceptable; back-fiducial makes it perfect |
| Fan (overlapping cards) | per-pixel handles overlap naturally | render a quad per detected fiducial; overlapping quads Z-sort correctly | clean — this is where per-pixel + multi-quad shine together |
| Stack | env depth sees the top card; hidden cards irrelevant (not visible anyway) | top fiducial tracked | correct |
| **Finger over card** | per-pixel is *correct here* — the finger is nearer, occludes the card region naturally | flat quad would wrongly cover the finger → **so we let env depth win in finger regions** (min-depth composite: take the nearest of env-depth and quad) | **this is why we keep BOTH layers** — the quad gives crisp card edges, env depth restores fingers on top. Composite = `min(depth_env, depth_quad)` |

The finger-over-card case is the clearest justification for the hybrid: a pure fiducial quad would paint over your fingers; a pure env-depth map has mushy card edges. `min()`-compositing the two depth sources gives crisp card edges **and** correct fingers.

## 6. Full approach comparison

| Approach | Impl. complexity | GPU cost | Robustness (cards) | Latency | Quest 3 | Vision Pro | Android XR | Fit for us |
|---|---|---|---|---|---|---|---|---|
| **Depth API env depth** | Low (SDK feature) | Med (soft > hard) | Med — noisy thin/foil edges | ~1 frame (reprojected) | ✅ native | ❌ no equivalent per-pixel dynamic API | ✅ ARCore Jetpack XR Depth (moving to Beta) | **Base layer** |
| **Fiducial-tracked occluder quad** | Med (we build it; reuse identity pose) | Very low (1 quad, depth-only) | High — crisp, stable | tracks at fiducial rate, pose-interpolated | ✅ | ✅ (image/world anchor pose) | ✅ | **Reinforcement layer** |
| **Hand mesh occluder** | Low (SDK) | Low | n/a (hands) | low | ✅ | ✅ (ARKit hand mesh) | ✅ | Use for hands regardless |
| **Semantic/neural segmentation** (segment "card" pixels via CNN on passthrough camera) | High (train + run a model on Passthrough Camera API feed) | High (NPU/GPU, thermal) | Potentially highest edge quality; robust to foil/low-texture | Higher (inference) | ⚠️ possible via PCA, costly | ⚠️ camera access limited | ⚠️ | **Future upgrade**, not launch |
| **Object reconstruction / digital twin** | Med-High | Low (render a virtual card) | 100% stable (it's virtual) but loses "real card" magic | none | ✅ | ✅ (the VP path) | ✅ | **Cross-platform fallback** |
| **Stereo depth estimation (custom)** | Very High (reimplement what Depth API already does) | High | — | — | redundant | — | — | Rejected — don't rebuild the SDK |

## 7. Recommended shipping architecture (Quest 3 primary)

A **three-layer composite**, cheapest-reliable-first:

```
Per eye, per frame:
 1. Passthrough = underlay (real video behind everything)
 2. Depth prepass (write depth only, ColorMask 0, ZWrite On, queue < 2000):
      a. Hand mesh occluder      (first-party hand mesh)
      b. Fiducial card quad(s)   (one per tracked card; our pose)
      c. Depth API env-depth     (per-pixel; composited as min-depth with a,b)
    → net occluder depth = min( handMesh, cardQuad, envDepth )
 3. Render virtual world with ZTest LEqual → every fragment behind the
    occluder depth is discarded → passthrough shows through there.
 4. Near-field guard (<0.2 m): where card/hand depth < 0.2 m OR env-depth
    confidence low, switch that region to SelectivePassthrough SOFT blend
    (fade the virtual world out around the card) instead of hard depth test.
```

Why this is the right build:
- **Env depth** carries the general scene (arms, table, fingers, the messy dynamic stuff) with zero bespoke work.
- **Fiducial quad** guarantees the *card* — the one object the player scrutinizes — has crisp, stable, low-cost edges exactly where env depth is weakest, reusing pose we already compute for identity. One extra depth-only quad is essentially free GPU-wise.
- **`min()` composite** resolves finger-over-card correctly (env depth wins where a finger is nearer).
- **Soft-blend near-field guard** turns Depth API's documented <0.2 m failure into a *stylized magical shimmer* when a card is brought to the face — a feature, not a bug, and it matches Meta's own "fade/blend fallback" guidance.
- Everything is on-device, ~1-frame latency, inside the thermal budget (the card quad costs nothing; soft occlusion is the only real cost and we scope it to the card/hand region, not the whole scene).

## 8. Cross-platform variants

| Platform | Card-visibility approach | Notes |
|---|---|---|
| **Quest 3 / 3S** | Full §7 hybrid (env depth + fiducial quad + soft near-field) | Reference implementation. Depth API is Quest 3/3S only |
| **Android XR** (Galaxy XR etc.) | Same pattern: **ARCore for Jetpack XR Depth** (moving to Beta) provides per-pixel depth for the env layer; fiducial quad identical | Verify per-device depth quality; likely parity with Quest given Snapdragon XR2 lineage |
| **Apple Vision Pro** | **No per-pixel dynamic environment-depth occlusion API for arbitrary held objects.** ARKit gives Scene Reconstruction (static mesh) + hand mesh + image anchors, plus RealityKit `OcclusionMaterial`, but not Quest-style live held-object depth. → **Digital-twin path**: render the virtual card at the image-anchored pose; the "real card in a virtual world" mode is not achievable to the same fidelity today | Also gated by ~1 Hz image tracking → VP is the remote/VR-twin build regardless |
| **Mobile AR** | N/A visually — you're looking at a phone screen already showing the real card in the camera frame; ARCore Depth can occlude virtual monsters behind the hand if desired | The card is trivially "visible" because it's in the video feed |

## 9. Limitations & failure-mode handling (explicit)

| Limitation | Cause | Mitigation |
|---|---|---|
| **Card edges shimmer** | stereo env-depth on thin/low-texture edges | fiducial quad overrides the silhouette (primary reason it exists) |
| **Foil / glossy / reflective cards** | specular breaks stereo matching AND can wash out the fiducial | matte-finish sanctioned sleeves with printed fiducial border (product decision); quad relies on pose not texture, so it survives glare better than env depth |
| **Card < 0.2 m from face** | Depth API min range | soft-blend near-field guard (shimmer) |
| **Fiducial momentarily lost** (occlusion, blur, motion) | tracking dropout | fall back to env-depth-only for that card (still occludes, softer edges); pose-predict/interpolate across short gaps |
| **Card face-away** | back has no fiducial | env-depth-only, or print a back-side fiducial for full fidelity |
| **Low light** | both stereo depth and fiducial detection degrade | passthrough itself degrades too; raise in-game "arena glow" lighting; lean on quad pose-prediction |
| **Latency between real card motion and occlusion** | depth map reprojection + fiducial pose rate | quad is pose-interpolated to display rate; env-depth is timewarp-reprojected by the compositor |
| **GPU/thermal budget** | soft occlusion + vision + creatures at 90 Hz stereo | scope soft occlusion to card/hand ROI; hard occlusion elsewhere; fiducial reader on its own thread at 15–30 Hz with interpolation |

## 10. Implementation starting points (concrete)

- **Depth API + occlusion shaders:** Meta [Depth API Overview](https://developers.meta.com/horizon/documentation/unity/unity-depthapi-overview/) and [Occlusions doc](https://developers.meta.com/horizon/documentation/unity/unity-customize-passthrough-passthrough-occlusions/); open-source [Unity-DepthAPI (oculus-samples)](https://github.com/oculus-samples/Unity-DepthAPI); soft masking via the **SelectivePassthrough** sample.
- **Depth-only occluder material:** `ColorMask 0`, `ZWrite On`, `Queue < 2000` (Meta's exact recipe, §2A) — apply to hand mesh and to the fiducial card quad.
- **Fiducial pose:** Passthrough Camera API + ArUco/OpenCV (community stacks: [QuestCameraKit](https://github.com/xrdevrob/QuestCameraKit), [QuestArUcoMarkerTracking](https://github.com/TakashiYoshinaga/QuestArUcoMarkerTracking)) → feed pose to the occluder quad and to card identity simultaneously.
- **Hands removal reference** (inverse of what we want, but shows the depth-map hand mask plumbing): Meta [Depth API Hands Removal](https://developers.meta.com/horizon/documentation/unity/unity-depthapi-hands-removal/).
- **Android XR depth:** [ARCore for Jetpack XR Depth](https://developer.android.com/develop/xr/jetpack-xr-sdk/arcore/depth).
- **Vision Pro:** [visionOS Scene Reconstruction / ARKit](https://developer.apple.com/visionos/) + RealityKit `OcclusionMaterial` (digital-twin path).

## 11. One-paragraph verdict

Ship the **hybrid depth-composite on Quest 3**: Depth API environment depth as the geometry-agnostic base (free handling of hands, arms, table, fingers), reinforced by a **fiducial-tracked depth-only quad per card** that reuses the pose we already compute for identity — giving the card crisp, stable, near-zero-cost edges precisely where stereo depth is weakest — composited by `min()` so fingers-over-card resolve correctly, with a **soft-blend near-field guard** turning the <0.2 m depth limit into a stylized shimmer. This is all documented, on-device, ~1-frame-latency, and thermally scoped. Android XR mirrors it via ARCore Jetpack XR depth; Vision Pro and mobile fall back to the **digital-twin** render (which we build anyway for remote play). Neural card segmentation on the Passthrough Camera feed is the identified future upgrade for foil/edge quality, not a launch dependency.

---

## Sources
- Meta [Depth API Overview](https://developers.meta.com/horizon/documentation/unity/unity-depthapi-overview/) (per-eye/per-frame depth; dynamic-vs-Scene-API table; 0.2 m limit; Quest 3/3S only)
- Meta [Passthrough Occlusions in Unity](https://developers.meta.com/horizon/documentation/unity/unity-customize-passthrough-passthrough-occlusions/) (depth-only occluder recipe: ColorMask 0 / ZWrite / queue<2000; SelectivePassthrough soft masking; layer compositing)
- Meta [Depth API Hands Removal](https://developers.meta.com/horizon/documentation/unity/unity-depthapi-hands-removal/) · [Mesh + Depth blog](https://developers.meta.com/horizon/blog/mesh-depth-api-meta-quest-3-developers-mixed-reality/)
- Open source: [Unity-DepthAPI](https://github.com/oculus-samples/Unity-DepthAPI) · [QuestCameraKit](https://github.com/xrdevrob/QuestCameraKit) · [QuestArUcoMarkerTracking](https://github.com/TakashiYoshinaga/QuestArUcoMarkerTracking)
- [ARCore for Jetpack XR — Depth](https://developer.android.com/develop/xr/jetpack-xr-sdk/arcore/depth) · [ARCore Depth (AR Foundation)](https://developers.google.com/ar/develop/unity-arf/depth/developer-guide)
- Apple [visionOS / ARKit Scene Reconstruction](https://developer.apple.com/visionos/) · [applying mesh to surroundings (OcclusionMaterial)](https://developer.apple.com/documentation/visionOS/applying-mesh-to-real-world-surroundings)
