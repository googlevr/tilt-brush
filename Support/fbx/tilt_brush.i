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

// For usage, see Support/fbx/README.md

%rename("%s") FbxGeometryBase::ComputeBBox;
%fbximmutable(FbxGeometryBase::BBoxMin);
%fbximmutable(FbxGeometryBase::BBoxMax);

%rename("%s") FbxMesh::SplitPoints;

// It is valid to pass in a null FbxAnimStack
// (see http://help.autodesk.com/view/FBX/2020/ENU/?guid=FBX_Developer_Help_cpp_ref_class_fbx_node_html)
#ifndef SWIG_GENERATING_TYPEDEFS
%apply FbxAnimStack * MAYBENULL { FbxAnimStack *pAnimStack };
#endif
%rename("%s") FbxNode::ConvertPivotAnimationRecursive;

%rename("%s") FbxNode::SetGeometricTranslation;
%rename("%s") FbxNode::SetGeometricRotation;
%rename("%s") FbxNode::SetGeometricScaling;
%rename("%s") FbxNode::GetMaterialCount;
%rename("%s") FbxNode::GetQuaternionInterpolation;
%rename("%s") FbxNode::SetQuaternionInterpolation;

%extend FbxSurfaceLambert {
  static FbxSurfaceLambert *fromMaterial(FbxSurfaceMaterial *m) {
    return static_cast<FbxSurfaceLambert *>(m);
  }
}

// These are to allow us to set values on properties - the Unity wrappers only
// support setting floats.
%rename("%s") FbxProperty::SetFloat;
%rename("%s") FbxProperty::SetString;
%rename("%s") FbxProperty::SetDouble;
%rename("%s") FbxProperty::SetBool;
%rename("%s") FbxProperty::SetColor;
%rename("%s") FbxProperty::SetInt;

%extend FbxProperty {
  void SetFloat(float value) { $self->Set<FbxString>(value); }
  void SetString(FbxString value) { $self->Set<FbxString>(value); }
  void SetDouble(FbxDouble value) { $self->Set<FbxDouble>(value); }
  void SetBool(FbxBool value) { $self->Set<FbxBool>(value); }
  void SetColor(FbxColor value) { $self->Set<FbxColor>(value); }
  void SetInt(int value) { $self->Set<int>(value); }
}

// Unity's wrappers handle templates slightly different from the
// original Tilt Brush wrappers, and I was having trouble doing it
// their way:
//   %template("GetSrcObject_FileTexture") FbxProperty::GetSrcObject<FbxFileTexture>;
// This is a bit brute-force but it works.
%extend FbxProperty {
  FbxFileTexture* GetSrcObject_FileTexture() {
    return $self->GetSrcObject<FbxFileTexture>();
  }
}

%extend FbxPropertyT<FbxDouble3> {
  FbxFileTexture* GetSrcObject_FileTexture() {
    return $self->GetSrcObject<FbxFileTexture>();
  }
}

%include "arrays_csharp.i"
%typemap(imtype) float * "System.IntPtr"
%typemap(cstype) float * "System.IntPtr"
%typemap(csin)   float * "$csinput"

%inline %{

// f is Vector3 array
// Mesh is preinitialized with the number of vertices
void SetControlPoints(FbxGeometryBase *mesh, float *f) {
  FbxVector4 *v = mesh->GetControlPoints();
  int n = mesh->GetControlPointsCount();
  for (int i = 0; i < n; i++) {
    v[i][0] = f[i*3];
    v[i][1] = f[i*3+1];
    v[i][2] = f[i*3+2];
    v[i][3] = 1.f;
  }
}

// f is pre-allocated Vector3 array
void GetControlPoints(FbxGeometryBase *mesh, float *f) {
  FbxVector4 *v = mesh->GetControlPoints();
  int n = mesh->GetControlPointsCount();
  for (int i = 0; i < n; i++) {
    f[i*3]   = static_cast<float>(v[i][0]);
    f[i*3+1] = static_cast<float>(v[i][1]);
    f[i*3+2] = static_cast<float>(v[i][2]);
  }
}

// f is Vector3 array
// Element array is preinitialized with the number of elements
void CopyVector3ToFbxVector4(FbxLayerElementArrayTemplate<FbxVector4> *layerElement, float *f) {
  FbxVector4 *v = nullptr;
  int n = layerElement->GetCount();
  v = layerElement->GetLocked(v);
  for (int i = 0; i < n; i++) {
    v[i][0] = f[i*3];
    v[i][1] = f[i*3+1];
    v[i][2] = f[i*3+2];
    v[i][3] = 1.f;
  }
  layerElement->Release(&v, v);
}

// f is Unity Color (float4) array, ordered abgr
// Element array is preinitialized with the number of elements
void CopyColorToFbxColor(FbxLayerElementArrayTemplate<FbxColor> *layerElement, float *f) {
  FbxColor *colors = nullptr;
  int n = layerElement->GetCount();
  colors = layerElement->GetLocked(colors);
  for (int i = 0; i < n; i++) {
    colors[i][0] = f[i*4];
    colors[i][1] = f[i*4+1];
    colors[i][2] = f[i*4+2];
    colors[i][3] = f[i*4+3];
  }
  layerElement->Release(&colors, colors);
}

void CopyVector4ToFbxVector4(FbxLayerElementArrayTemplate<FbxVector4> *layerElement, float *f) {
  FbxVector4 *v = nullptr;
  int n = layerElement->GetCount();
  v = layerElement->GetLocked(v);
  for (int i = 0; i < n; i++) {
    v[i][0] = f[i*4];
    v[i][1] = f[i*4+1];
    v[i][2] = f[i*4+2];
    v[i][3] = f[i*4+3];
  }
  layerElement->Release(&v, v);
}

void CopyVector2ToFbxVector2(FbxLayerElementArrayTemplate<FbxVector2> *layerElement, float *f) {
  FbxVector2 *v = nullptr;
  int n = layerElement->GetCount();
  v = layerElement->GetLocked(v);
  for (int i = 0; i < n; i++) {
    v[i][0] = f[i*2];
    v[i][1] = f[i*2+1];
  }
  layerElement->Release(&v, v);
}

// Copy FbxVector2 to pre-allocated array of Vector2
void CopyFbxVector2ToVector2(FbxLayerElementArrayTemplate<FbxVector2> *layerElement, float *f) {
  int n = layerElement->GetCount();
  FbxVector2 *v = nullptr;
  v = layerElement->GetLocked(v);
  for (int i = 0; i < n; i++) {
    f[i*2]   = static_cast<float>(v[i][0]);
    f[i*2+1] = static_cast<float>(v[i][1]);
  }
  layerElement->Release(&v, v);
}

// Copy x,y,z components of FbxVector4 to Vector3
void CopyFbxVector4ToVector3(FbxLayerElementArrayTemplate<FbxVector4> *layerElement, float *f) {
  int n = layerElement->GetCount();
  FbxVector4 *v = nullptr;
  v = layerElement->GetLocked(v);
  for (int i = 0; i < n; i++) {
    f[i*3]   = static_cast<float>(v[i][0]);
    f[i*3+1] = static_cast<float>(v[i][1]);
    f[i*3+2] = static_cast<float>(v[i][2]);
  }
  layerElement->Release(&v, v);
}

%}
