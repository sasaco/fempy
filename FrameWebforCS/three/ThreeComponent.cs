using Assimp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using THREE;

namespace FrameWebforCS.three
{
    public partial class ThreeComponent : UserControl
    {
        public ThreeComponent()
        {
            InitializeComponent();
        }
        public override void Load(GLControl glControl)
        {
            base.Load(glControl);

            InitRenderer();

            InitCamera();

            InitCameraController();

            scene.Background = Color.Hex(0xffffff);

            var axes = new AxesHelper(20);

            scene.Add(axes);

            var planeGeometry = new PlaneGeometry(60, 20);
            var planeMaterial = new MeshBasicMaterial() { Color = Color.Hex(0xcccccc) };
            var plane = new Mesh(planeGeometry, planeMaterial);

            plane.Rotation.X = (float)(-0.5 * Math.PI);
            plane.Position.Set(15, 0, 0);

            scene.Add(plane);

            // create a cube
            var cubeGeometry = new BoxGeometry(4, 4, 4);
            var cubeMaterial = new MeshBasicMaterial() { Color = Color.Hex(0xff0000), Wireframe = true };
            var cube = new Mesh(cubeGeometry, cubeMaterial);

            // position the cube
            cube.Position.Set(-4, 3, 0);

            // add the cube to the scene

            scene.Add(cube);

            //      // create a sphere
            var sphereGeometry = new SphereGeometry(4, 20, 20);
            var sphereMaterial = new MeshBasicMaterial() { Color = Color.Hex(0x7777ff), Wireframe = true };
            var sphere = new Mesh(sphereGeometry, sphereMaterial);

            //      // position the sphere
            sphere.Position.Set(20, 4, 2);

            //      // add the sphere to the scene
            scene.Add(sphere);

        }
        public override void Render()
        {
            controls.Update();
            this.renderer.Render(scene, camera);
        }
    }
}
