using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Short first-run tutorial (GDD 10.5): a few cards, one idea each, with the game paused.
/// Each card highlights the HUD control it talks about with a pulsing ring (joystick, buttons,
/// smog bar). Advance with a tap, click, Space or the gamepad's A button; it can be skipped.
/// It only shows once (PlayerPrefs); <see cref="ResetSeen"/> enables it again.
/// Builds its UI in code inside the HUD's UIDocument; styles live in GameHUD.uss.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class TutorialController : MonoBehaviour
{
    private const string PrefSeen = "tutorial.seen";

    [Tooltip("Show it even if already seen (for testing in the Editor).")]
    [SerializeField] private bool _alwaysShow;
    [Tooltip("Seconds before the first card, so the city can be seen.")]
    [SerializeField] private float _startDelay = 0.6f;

    private struct Step
    {
        public string Title;
        public string Touch;
        public string Keys;
        public string Target; // name of the element to highlight (may live in another UIDocument on the same panel)
    }

    private static readonly Step[] Steps =
    {
        new Step
        {
            Title = "Hello, pilot!",
            Touch = "The city is full of smog. Your drone Verdant-1 can clean it up.",
            Keys = "The city is full of smog. Your drone Verdant-1 can clean it up.",
        },
        new Step
        {
            Title = "Fly",
            Touch = "Push the joystick up to move forward and sideways to turn. Use ▲ and ▼ to climb and descend.",
            Keys = "W and S move forward and back, A and D turn. Climb with Space and descend with Shift.",
            Target = "touch-zone-left",
        },
        new Step
        {
            Title = "Purifier Beam",
            Touch = "Aim at a smog cloud and hold BEAM to break it up.",
            Keys = "Aim at a smog cloud and hold the left mouse button to break it up.",
            Target = "btn-beam",
        },
        new Step
        {
            Title = "Seeds",
            Touch = "Find the green spots and press SEED. Trees clean the air on their own.",
            Keys = "Find the green spots and right-click. Trees clean the air on their own.",
            Target = "btn-seed",
        },
        new Step
        {
            Title = "Your mission",
            Touch = "Bring the smog down to 0% before time runs out. You need both the beam and the seeds!",
            Keys = "Bring the smog down to 0% before time runs out. You need both the beam and the seeds!",
            Target = "smog-meter",
        },
    };

    private VisualElement _root;
    private VisualElement _overlay;
    private VisualElement _ring;
    private Label _title;
    private Label _body;
    private Label _counter;
    private Button _next;
    private Button _skip;
    private IVisualElementScheduledItem _pulse;
    private int _step = -1;
    private float _openAt = -1f;

    public bool IsOpen => _step >= 0;

    /// <summary>Makes the tutorial show again on the next run.</summary>
    public static void ResetSeen() => PlayerPrefs.DeleteKey(PrefSeen);

    private void OnEnable()
    {
        _root = GetComponent<UIDocument>().rootVisualElement;
        if (!_alwaysShow && PlayerPrefs.GetInt(PrefSeen, 0) == 1) return;
        Build();
        _openAt = Time.realtimeSinceStartup + _startDelay;
    }

    private void OnDisable()
    {
        _pulse?.Pause();
        if (IsOpen) Time.timeScale = 1f;
    }

    private void Update()
    {
        if (_openAt > 0f && Time.realtimeSinceStartup >= _openAt)
        {
            _openAt = -1f;
            Show(0);
        }
        if (!IsOpen) return;

        // The game waits while the player reads (pause also sets timeScale; here it is held at 0)
        Time.timeScale = 0f;
        PlaceRing();

        bool keyNext = (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame))
                       || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
        if (keyNext) Next();
    }

    private void Build()
    {
        _overlay = new VisualElement { name = "tutorial-overlay" };
        _overlay.AddToClassList("tutorial-overlay");
        _overlay.style.display = DisplayStyle.None;
        // A tap anywhere advances
        _overlay.RegisterCallback<PointerUpEvent>(_ => Next());

        _ring = new VisualElement { pickingMode = PickingMode.Ignore };
        _ring.AddToClassList("tutorial-ring");
        _overlay.Add(_ring);

        var card = new VisualElement();
        card.AddToClassList("tutorial-card");
        _counter = new Label();
        _counter.AddToClassList("tutorial-card__counter");
        _title = new Label();
        _title.AddToClassList("tutorial-card__title");
        _body = new Label();
        _body.AddToClassList("tutorial-card__body");

        var row = new VisualElement();
        row.AddToClassList("tutorial-card__row");
        _skip = new Button(Finish) { text = "SKIP" };
        _skip.AddToClassList("menu-button");
        _skip.AddToClassList("menu-button--ghost");
        _skip.AddToClassList("tutorial-card__button");
        _next = new Button(Next) { text = "NEXT" };
        _next.AddToClassList("menu-button");
        _next.AddToClassList("menu-button--primary");
        _next.AddToClassList("tutorial-card__button");
        // Taps on the buttons must not also count as a "tap anywhere"
        _skip.RegisterCallback<PointerUpEvent>(e => e.StopPropagation());
        _next.RegisterCallback<PointerUpEvent>(e => e.StopPropagation());
        row.Add(_skip);
        row.Add(_next);

        card.Add(_counter);
        card.Add(_title);
        card.Add(_body);
        card.Add(row);
        _overlay.Add(card);

        // On top of the whole HUD except the fade
        VisualElement hudRoot = _root.Q<VisualElement>("game-hud-root") ?? _root;
        VisualElement fade = hudRoot.Q<VisualElement>("fade");
        if (fade != null) hudRoot.Insert(hudRoot.IndexOf(fade), _overlay);
        else hudRoot.Add(_overlay);

        _pulse = _ring.schedule.Execute(() => _ring.ToggleInClassList("tutorial-ring--pulse")).Every(450);
    }

    private void Show(int index)
    {
        _step = index;
        Step step = Steps[index];
        bool touch = Application.isMobilePlatform || Touchscreen.current != null;
        _counter.text = $"{index + 1} / {Steps.Length}";
        _title.text = step.Title;
        _body.text = touch ? step.Touch : step.Keys;
        _next.text = index == Steps.Length - 1 ? "LET'S PLAY!" : "NEXT";
        _overlay.style.display = DisplayStyle.Flex;
        PlaceRing();
    }

    private void Next()
    {
        if (!IsOpen) return;
        if (_step + 1 < Steps.Length) Show(_step + 1);
        else Finish();
    }

    private void Finish()
    {
        _step = -1;
        _overlay.style.display = DisplayStyle.None;
        Time.timeScale = 1f;
        PlayerPrefs.SetInt(PrefSeen, 1);
        PlayerPrefs.Save();
    }

    // Ring around the control the card talks about (hidden if not visible, e.g. without a touchscreen)
    private void PlaceRing()
    {
        VisualElement target = FindTarget(Steps[_step].Target);
        bool visible = target != null && target.resolvedStyle.display != DisplayStyle.None
                       && target.worldBound.width > 1f && IsShown(target);
        _ring.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        if (!visible) return;

        Rect bounds = _overlay.WorldToLocal(target.worldBound);
        const float pad = 18f;
        _ring.style.left = bounds.xMin - pad;
        _ring.style.top = bounds.yMin - pad;
        _ring.style.width = bounds.width + pad * 2f;
        _ring.style.height = bounds.height + pad * 2f;
    }

    private VisualElement FindTarget(string elementName)
    {
        if (string.IsNullOrEmpty(elementName) || _root.panel == null) return null;
        return _root.panel.visualTree.Q<VisualElement>(elementName);
    }

    private static bool IsShown(VisualElement element)
    {
        for (VisualElement e = element; e != null; e = e.parent)
        {
            if (e.resolvedStyle.display == DisplayStyle.None) return false;
        }
        return true;
    }
}
