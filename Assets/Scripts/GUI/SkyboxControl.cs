// Copyright 2020 The Tilt Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using UnityEngine;

namespace TiltBrush {
  public class SkyboxControl : UIComponent {
    [SerializeField] private float m_HoverVisualAdjust;

    private Renderer m_Renderer;
    private SphereCollider m_SphereCollider;
    private Quaternion m_SkyboxHandleGrabStart;
    private Vector3 m_SkyboxHandleGrabOffset;

    override protected void Awake() {
      base.Awake();
      SceneSettings.m_Instance.GradientActiveChanged += OnGradientActiveChanged;
      SceneSettings.m_Instance.SkyboxChanged += OnSkyboxChanged;

      m_Renderer = GetComponent<Renderer>();
      m_SphereCollider = m_Collider as SphereCollider;
      SceneSettings.m_Instance.GradientOrientation = Quaternion.identity;
      App.Scene.PoseChanged += (TrTransform prev, TrTransform current) => {
        transform.rotation = current.rotation * SceneSettings.m_Instance.GradientOrientation;
        if (SceneSettings.m_Instance.InGradient) {
          RenderSettings.skybox.SetVector("_GradientDirection", transform.up);
        }
      };
    }

    override protected void OnDestroy() {
      base.OnDestroy();
      SceneSettings.m_Instance.GradientActiveChanged -= OnGradientActiveChanged;
      SceneSettings.m_Instance.SkyboxChanged -= OnSkyboxChanged;
    }

    override public void SetColor(Color color) {
      m_Renderer.material.SetColor("_Tint", color);
    }

    override public void ButtonPressed(RaycastHit hitInfo) {
      m_SkyboxHandleGrabStart = transform.rotation;
      m_SkyboxHandleGrabOffset = IntersectionPointOnBackside(hitInfo.point) - transform.position;
    }

    override public void ButtonHeld(RaycastHit hitInfo) {
      // Calculate dragged distance.
      Vector3 endRot = IntersectionPointOnBackside(hitInfo.point) - transform.position;
      SetRotation(Quaternion.FromToRotation(m_SkyboxHandleGrabOffset, endRot) *
          m_SkyboxHandleGrabStart);
    }

    override public void ButtonReleased() {
      SketchMemoryScript.m_Instance.PerformAndRecordCommand(new ModifySkyboxCommand(
          SceneSettings.m_Instance.SkyColorA, SceneSettings.m_Instance.SkyColorB,
          SceneSettings.m_Instance.GradientOrientation, final: true));
    }

    override public bool CalculateReticleCollision(Ray castRay, ref Vector3 pos, ref Vector3 forward) {
      float t0, t1;
      Vector3 rayOrigin = castRay.origin - castRay.direction * 100.0f;
      if (MathUtils.RaySphereIntersection(rayOrigin, castRay.direction,
          transform.position, m_SphereCollider.radius, out t0, out t1)) {
        pos = rayOrigin + (castRay.direction * t1);
        forward = (transform.position - pos).normalized;
        Vector3 vScoot = forward * m_HoverVisualAdjust;
        pos += vScoot;
        return true;
      }
      return false;
    }

    void Update() {
      if (transform.hasChanged) {
        MaintainSceneSpaceRotation();
        transform.hasChanged = false;
      }
    }

    Vector3 IntersectionPointOnBackside(Vector3 hitPoint) {
      Vector3 meshForward = m_Manager.GetPanelForPopUps().m_Mesh.transform.forward;
      Vector3 rayOrigin = hitPoint - meshForward * 100.0f;

      float t0, t1;
      if (MathUtils.RaySphereIntersection(rayOrigin, meshForward,
          transform.position, m_SphereCollider.radius, out t0, out t1)) {
        return rayOrigin + (meshForward * t1);
      }
      return Vector3.zero;
    }

    void SetRotation(Quaternion rot) {
      transform.rotation = rot;
      SketchMemoryScript.m_Instance.PerformAndRecordCommand(new ModifySkyboxCommand(
          SceneSettings.m_Instance.SkyColorA, SceneSettings.m_Instance.SkyColorB,
          Quaternion.Inverse(App.Scene.Pose.rotation) * rot));
    }

    void MaintainSceneSpaceRotation() {
      transform.localRotation =
          Quaternion.Inverse(transform.parent.rotation) *
          App.Scene.Pose.rotation *
          SceneSettings.m_Instance.GradientOrientation;
    }

    void OnGradientActiveChanged() {
      m_Renderer.material.SetColor("_ColorA", SceneSettings.m_Instance.SkyColorA);
      m_Renderer.material.SetColor("_ColorB", SceneSettings.m_Instance.SkyColorB);

      if (SceneSettings.m_Instance.InGradient) {
        m_Renderer.material.SetVector("_GradientDirection", Vector3.up);
      } else {
        MaintainSceneSpaceRotation();
      }
    }

    void OnSkyboxChanged() {
      m_Renderer.material.SetColor("_ColorA", SceneSettings.m_Instance.SkyColorA);
      m_Renderer.material.SetColor("_ColorB", SceneSettings.m_Instance.SkyColorB);
      MaintainSceneSpaceRotation();
    }
  }
} // namespace TiltBrush
