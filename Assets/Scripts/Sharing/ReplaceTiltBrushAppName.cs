using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush {
public class ReplaceTiltBrushAppName : MonoBehaviour {
  private void Start() {
    var server = gameObject.GetComponent<HttpFileServer>();
    server?.AddSubstitution("Tilt Brush", App.kAppDisplayName);
  }
}
}
