-----------------------
How to use this viewer
-----------------------

1) Export geometry from Tilt Brush
   * Open Tilt Brush in the Unity Editor
   * On /App/Config, click the "Copy Assets to glTF" check box
   * Run Tilt Brush
   * Load a sketch
   * Back in the Editor, click Tilt > glTF > Export Brush Strokes to glTF

2) Copy Documents/Tilt Brush/Exports/<your sketch> folder to the ./geom directory in this folder.

3) Run the server:
cd <Tilt Brush Root>/Support
python bin/gltfViewer/serve.py 9011

4) View your glTF file:
http://localhost:9011/gltfViewer.html?uri=/bin/gltfViewer/geom/ExampleSketch/Untitled.gltf
