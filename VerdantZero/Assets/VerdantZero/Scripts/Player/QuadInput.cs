using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Quadcopter input (GDD 6.2). Combines keyboard, mouse and gamepad with the values from the
/// HUD touch controls (Set* methods). The lateral stick turns the drone and the vertical one moves it forward and back; the camera follows from behind.
/// Keyboard: WASD move, Space climb, Left Shift descend, left click Beam, right click Seed.
/// Gamepad (Bluetooth or USB, GDD 10.4): left stick move; right stick, D-pad or RB/LB climb and descend;
/// RT or A Beam; LT or X Seed; Start pause (see PauseMenuController).
/// Mouse clicks do not count when they land on a HUD control (<see cref="MouseBlocker"/>): in the
/// browser, pressing the SEED button (with the mouse or a finger, which the browser also sends as
/// mouse) fired the Beam at the same time, and with the Beam active the seed is not launched.
/// </summary>
public class QuadInput : MonoBehaviour
{
    private InputAction _move;
    private InputAction _climb;
    private InputAction _beam;
    private InputAction _seed;
    private InputAction _mouseBeam;
    private InputAction _mouseSeed;

    private Vector2 _externalMove;
    private float _externalClimb;
    private bool _externalBeam;
    private bool _externalSeed;

    /// <summary>Horizontal movement: x = lateral, y = forward/back.</summary>
    public Vector2 Move { get; private set; }

    /// <summary>Climb (+1) / descend (-1).</summary>
    public float Climb { get; private set; }

    /// <summary>Purifier Beam pressed (held to fire).</summary>
    public bool Beam { get; private set; }

    /// <summary>Seed button pressed (a seed is launched on press).</summary>
    public bool Seed { get; private set; }

    /// <summary>Returns true if the screen position is over a HUD control; clicks are ignored there.</summary>
    public System.Func<Vector2, bool> MouseBlocker { get; set; }

    /// <summary>When true all input is ignored (e.g. when the run ends).</summary>
    public bool Locked { get; set; }

    // The on-screen sticks call these methods.
    public void SetMove(Vector2 value) => _externalMove = value;
    public void SetClimb(float value) => _externalClimb = value;
    public void SetBeam(bool value) => _externalBeam = value;
    public void SetSeed(bool value) => _externalSeed = value;

    private void Awake()
    {
        _move = new InputAction("Move", InputActionType.Value);
        _move.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        _move.AddBinding("<Gamepad>/leftStick").WithProcessor("stickDeadzone");

        _climb = new InputAction("Climb", InputActionType.Value);
        _climb.AddCompositeBinding("1DAxis")
            .With("Positive", "<Keyboard>/space")
            .With("Negative", "<Keyboard>/leftShift");
        _climb.AddBinding("<Gamepad>/rightStick/y").WithProcessor("axisDeadzone");
        _climb.AddCompositeBinding("1DAxis")
            .With("Positive", "<Gamepad>/dpad/up")
            .With("Negative", "<Gamepad>/dpad/down");
        _climb.AddCompositeBinding("1DAxis")
            .With("Positive", "<Gamepad>/rightShoulder")
            .With("Negative", "<Gamepad>/leftShoulder");

        _beam = new InputAction("Beam", InputActionType.Button);
        _beam.AddBinding("<Gamepad>/rightTrigger");
        _beam.AddBinding("<Gamepad>/buttonSouth");

        _seed = new InputAction("Seed", InputActionType.Button);
        _seed.AddBinding("<Gamepad>/leftTrigger");
        _seed.AddBinding("<Gamepad>/buttonWest");

        // The mouse is handled separately so it can be ignored over HUD controls
        _mouseBeam = new InputAction("MouseBeam", InputActionType.Button, "<Mouse>/leftButton");
        _mouseSeed = new InputAction("MouseSeed", InputActionType.Button, "<Mouse>/rightButton");
    }

    private void OnEnable()
    {
        _move.Enable();
        _climb.Enable();
        _beam.Enable();
        _seed.Enable();
        _mouseBeam.Enable();
        _mouseSeed.Enable();
    }

    private void OnDisable()
    {
        _move.Disable();
        _climb.Disable();
        _beam.Disable();
        _seed.Disable();
        _mouseBeam.Disable();
        _mouseSeed.Disable();
    }

    private void OnDestroy()
    {
        _move.Dispose();
        _climb.Dispose();
        _beam.Dispose();
        _seed.Dispose();
        _mouseBeam.Dispose();
        _mouseSeed.Dispose();
    }

    // Read once per frame; FixedUpdate only consumes the cached values.
    private void Update()
    {
        if (Locked)
        {
            Move = Vector2.zero;
            Climb = 0f;
            Beam = false;
            Seed = false;
            return;
        }

        Move = Vector2.ClampMagnitude(_move.ReadValue<Vector2>() + _externalMove, 1f);
        Climb = Mathf.Clamp(_climb.ReadValue<float>() + _externalClimb, -1f, 1f);
        bool mouseFree = !MouseOverHud();
        Beam = _beam.IsPressed() || (mouseFree && _mouseBeam.IsPressed()) || _externalBeam;
        Seed = _seed.IsPressed() || (mouseFree && _mouseSeed.IsPressed()) || _externalSeed;
    }

    private bool MouseOverHud()
    {
        Mouse mouse = Mouse.current;
        return mouse != null && MouseBlocker != null && MouseBlocker(mouse.position.ReadValue());
    }
}
