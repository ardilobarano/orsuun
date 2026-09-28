using UnityEngine;
using UnityEngine.Rendering;

namespace Orsuun.Client
{
    /// <summary>
    /// A 3D place shown in the lane's stead (RiverScene, TownScene): while it shows, the lane's cameras rest and the sun
    /// and the ambient light are the place's; Leave puts the lane's back.
    /// </summary>
    public sealed class PlaceMood
    {
        private bool _in;
        private Light _sun;
        private Quaternion _sunRotation;
        private Color _sunColor, _ambient;
        private float _sunIntensity;
        private SphericalHarmonicsL2 _ambientProbe;
        private bool _fog;
        private Camera _laneCamera, _backdropCamera;

        public void Enter(Quaternion sun, Color sunColor, float intensity, Color ambient)
        {
            if (_in) return;
            _in = true;
            _laneCamera = GameObject.Find("LaneCamera")?.GetComponent<Camera>();
            _backdropCamera = GameObject.Find("BackdropCamera")?.GetComponent<Camera>();
            if (_laneCamera != null) _laneCamera.enabled = false;
            if (_backdropCamera != null) _backdropCamera.enabled = false;
            _sun = GameObject.Find("Sun")?.GetComponent<Light>();
            if (_sun != null)
            {
                _sunRotation = _sun.transform.rotation;
                _sunColor = _sun.color;
                _sunIntensity = _sun.intensity;
                _sun.transform.rotation = sun;
                _sun.color = sunColor;
                _sun.intensity = intensity;
            }
            _ambient = RenderSettings.ambientLight;
            _ambientProbe = RenderSettings.ambientProbe;
            // The lane's haze (LaneView.ApplyFog) is not the place's.
            _fog = RenderSettings.fog;
            RenderSettings.fog = false;
            RenderSettings.ambientLight = ambient;
            var probe = new SphericalHarmonicsL2();
            probe.AddAmbientLight(ambient);
            RenderSettings.ambientProbe = probe;
        }

        public void Leave()
        {
            if (!_in) return;
            _in = false;
            if (_laneCamera != null) _laneCamera.enabled = true;
            if (_backdropCamera != null) _backdropCamera.enabled = true;
            if (_sun != null)
            {
                _sun.transform.rotation = _sunRotation;
                _sun.color = _sunColor;
                _sun.intensity = _sunIntensity;
            }
            RenderSettings.ambientLight = _ambient;
            RenderSettings.ambientProbe = _ambientProbe;
            RenderSettings.fog = _fog;
        }
    }
}
