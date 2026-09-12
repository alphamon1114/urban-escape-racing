using UnityEngine;
namespace UrbanEscape
{
    public sealed class DrivingHud : MonoBehaviour
    {
        public SedanController car;
        void OnGUI()
        {
            GUI.Box(new Rect(18, 18, 390, 125), "URBAN ESCAPE | DRIVING PROTOTYPE");
            GUI.Label(new Rect(32, 48, 360, 25), $"{car.SpeedKph:0} km/h    |    AWD    |    Steer {car.SteeringAngle:0} deg");
            GUI.Label(new Rect(32, 77, 365, 25), "W Accelerate   S Brake / Reverse   A/D Steer");
            GUI.Label(new Rect(32, 104, 365, 25), "Space Handbrake   R Reset   |   Slow down to turn");
        }
    }
}
