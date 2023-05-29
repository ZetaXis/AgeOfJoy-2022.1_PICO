using System.Collections;
using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;
using CleverCrow.Fluid.BTs.Tasks;
using CleverCrow.Fluid.BTs.Trees;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ConfigurationHelper
{
    private GlobalConfiguration globalConfiguration = null;
    private RoomConfiguration roomConfiguration = null;

    public ConfigurationHelper(GlobalConfiguration globalConfiguration, RoomConfiguration roomConfiguration)
    {
        this.roomConfiguration = roomConfiguration;
        this.globalConfiguration = globalConfiguration;

        if (globalConfiguration == null)
        {
            GameObject GlobalConfigurationGameObject = GameObject.Find("GlobalConfiguration");
            this.globalConfiguration = GlobalConfigurationGameObject.GetComponent<GlobalConfiguration>();
        }
        if (this.globalConfiguration == null)
            throw new ArgumentException("[ConfigurationHelper] global configuration not found.");

        ConfigManager.WriteConsole($"[ConfigurationHelper] global: {this.globalConfiguration.yamlPath} room: {this.roomConfiguration?.yamlPath}");
    }

    public ConfigInformation getConfigInformation(bool isGlobal)
    {
        ConfigInformation config;

        if (!isGlobal && roomConfiguration == null)
            throw new ArgumentException("Room configuration is missing in cabinet configuration screen gameobject ");

        if (!isGlobal)
        {
            config = roomConfiguration.Configuration;

            if (config == null)
            {
                roomConfiguration.Configuration = ConfigInformation.newDefault();
                config = roomConfiguration.Configuration;
            }
        }
        else
        {
            config = globalConfiguration.Configuration;
            if (config == null)
            {
                globalConfiguration.Configuration = ConfigInformation.newDefault();
                config = globalConfiguration.Configuration;
            }
        }

        return config;
    }
    public bool CanConfigureRoom()
    {
        ConfigManager.WriteConsole($"[ConfigurationHelper.CanConfigureRoom] room: {roomConfiguration?.yamlPath}");
        return roomConfiguration != null;
    }

    public void Save(bool saveGlobal, ConfigInformation config)
    {
        string yamlPath;
        if (saveGlobal)
            yamlPath = globalConfiguration.yamlPath;
        else
            yamlPath = roomConfiguration.yamlPath;
        ConfigManager.WriteConsole($"[ConfigurationController] save configuration global:{saveGlobal} {config} to {yamlPath}");
        config.ToYaml(yamlPath); //saving the file will reload the configuration in GlobalConfiguration and RoomConfiguration by trigger.

        return;
    }


    public void Reset(bool isGlobal)
    {
        if (isGlobal)
        {
            globalConfiguration.Reset();
        }
        else
        {
            roomConfiguration.Reset();
        }
    }
}

public class ConfigurationController : MonoBehaviour
{
    public ScreenGenerator scr;
    public CoinSlotController CoinSlot;
    public InputActionMap actionMap;
    [Tooltip("The global action manager in the main rig. We will find one if not set.")]
    public InputActionManager inputActionManager;

    [Tooltip("We will find the correct one")]
    public ChangeControls changeControls;

    public CabinetsController cabinetsController;

    [Tooltip("Set only to change room configuration, if not setted will use the Global")]
    public RoomConfiguration roomConfiguration;
    [Tooltip("Set to change the Global or the system will find it")]
    public GlobalConfiguration globalConfiguration;

    public bool canChangeAudio = true;
    public bool canChangeNPC = true;
    public bool canChangeControllers = true;
    public bool canChangeCabinets = true;

    [Header("Tree")]
    [SerializeField]
    public BehaviorTree tree;

    private GenericMenu mainMenu;
    private enum StatusOptions
    {
        init,
        waitingForCoin,
        onMainMenu,
        onBoot,
        onNPCMenu,
        onAudio,
        onChangeMode,
        onChangeController,
        onChangeCabinets,
        onReset,
        exit
    }
    private StatusOptions status;
    private C64BootScreen bootScreen;
    private LibretroMameCore.Waiter onBootDelayWaiter;

    private string NPCStatus;
    private GenericOptions NPCStatusOptions;
    private GenericWindow npcWindow;

    private GenericBool isGlobalConfigurationWidget;
    private GenericWidgetContainer changeModeContainer, audioContainer, resetContainer, controllerContainer;
    private GenericLabel lblGameSelected;
    private GenericOptions controlMapGameId, controlMapMameControl;
    private GenericTimedLabel controlMapSavedLabel;
    private List<GenericOptions> controlMapRealControls;
    private ControlMapConfiguration controlMapConfiguration;

    private GameRegistry gameRegistry;
    private GenericWidgetContainer cabinetsToChangeContainer;
    private GenericOptions cabinetToReplace;
    private GenericOptions cabinetReplaced;


    private DefaultControlMap map;

    private ConfigurationHelper configHelper;


    // Start is called before the first frame update
    void Start()
    {
        map = new();
        ControlMapConfiguration conf = new DefaultControlMap();
        conf.AddMap("KEYB-UP", "keyboard-w");
        conf.AddMap("KEYB-DOWN", "keyboard-s");
        conf.AddMap("KEYB-LEFT", "keyboard-a");
        conf.AddMap("KEYB-RIGHT", "keyboard-d");
        actionMap = ControlMapInputAction.inputActionMapFromConfiguration(conf);

        if (changeControls == null)
        {
            GameObject player = GameObject.Find("OVRPlayerControllerGalery");
            changeControls = player.GetComponent<ChangeControls>();
        }
        GameObject inputActionManagerGameobject = GameObject.Find("Input Action Manager");
        inputActionManager = inputActionManagerGameobject.GetComponent<InputActionManager>();

        gameRegistry = GameObject.Find("RoomInit").GetComponent<GameRegistry>();

        StartCoroutine(run());
    }

    public void NPCScreen()
    {
        //set the init value
        string actualNPCStatus = ConfigInformation.NPC.validStatus[0];
        ConfigInformation config = configHelper.getConfigInformation(isGlobalConfigurationWidget.value);
        if (config?.npc != null)
            actualNPCStatus = config.npc.status;

        scr.Clear();
        npcWindow.Draw();

        //some help
        scr.Print(2, 16, "left/right to change");
        scr.Print(2, 17, "b to select and exit");

        ConfigManager.WriteConsole($"[ConfigurationController.GoToNPCConfiguration] NPC status: {actualNPCStatus}");
        NPCStatusOptions.SetCurrent(actualNPCStatus);
        NPCStatusOptions.Draw();
    }

    private void NPCSave()
    {
        bool isGlobal = isGlobalConfigurationWidget.value;
        ConfigInformation config = configHelper.getConfigInformation(isGlobal);
        config.npc = new();
        config.npc.status = NPCStatusOptions.GetSelectedOption();
        configHelper.Save(isGlobal, config);
    }

    public void resetWindowDraw()
    {
        scr.Clear();
        resetContainer.Draw();
        if (isGlobalConfigurationWidget.value)
        {
            scr.Print(2, 1, "Back global configuration");
            scr.Print(2, 2, "to default.");
            scr.Print(2, 3, "Lost all configured data.");
        }
        else
        {
            scr.Print(2, 1, "Room configuration will");
            scr.Print(2, 2, "be deleted.");
            scr.Print(2, 3, "You will lost all room configured data");
            scr.Print(2, 4, "except controllers information.");
        }
        scr.Print(2, 16, "up/down to change");
        scr.Print(2, 17, "b to select and exit");
    }

    public void resetSave()
    {
        configHelper.Reset(isGlobalConfigurationWidget.value);
        ConfigManager.WriteConsole($"[ConfigurationController.resetSave] Reset to default Global: {isGlobalConfigurationWidget.value}");
    }

    private void audioScreen()
    {
        //set the init value
        ConfigInformation config = configHelper.getConfigInformation(isGlobalConfigurationWidget.value);
        ConfigInformation.Background bkg = ConfigInformation.BackgroundDefault();
        ConfigInformation.Background ingamebkg = ConfigInformation.BackgroundInGameDefault();

        if (config?.audio?.background?.volume != null)
            ((GenericOptionsInteger)audioContainer.GetWidget("BackgroundVolume")).SetCurrent((int)config.audio.background.volume);
        else
            ((GenericOptionsInteger)audioContainer.GetWidget("BackgroundVolume")).SetCurrent((int)bkg.volume);

        if (config?.audio?.inGameBackground?.volume != null)
            ((GenericOptionsInteger)audioContainer.GetWidget("InGameBackgroundVolume")).SetCurrent((int)config.audio.inGameBackground.volume);
        else
            ((GenericOptionsInteger)audioContainer.GetWidget("InGameBackgroundVolume")).SetCurrent((int)ingamebkg.volume);

        if (config?.audio?.background?.muted != null)
            ((GenericBool)audioContainer.GetWidget("BackgroundMuted")).SetValue((bool)config.audio.background.muted);
        else
            ((GenericBool)audioContainer.GetWidget("BackgroundMuted")).SetValue((bool)bkg.muted);

        if (config?.audio?.inGameBackground?.muted != null)
            ((GenericBool)audioContainer.GetWidget("InGameBackgroundMuted")).SetValue((bool)config.audio.inGameBackground.muted);
        else
            ((GenericBool)audioContainer.GetWidget("InGameBackgroundMuted")).SetValue((bool)ingamebkg.muted);

        scr.Clear();
        audioContainer.Draw();
    }
    private void audioSave()
    {
        bool isGlobal = isGlobalConfigurationWidget.value;
        ConfigInformation config = configHelper.getConfigInformation(isGlobal);
        config.audio = new();
        config.audio.background = new(); //ConfigInformation.BackgroundDefault();
        config.audio.inGameBackground = new(); //ConfigInformation.BackgroundInGameDefault();
        config.audio.background.volume = (uint)((GenericOptionsInteger)audioContainer.GetWidget("BackgroundVolume")).GetSelectedOption();
        config.audio.background.muted = ((GenericBool)audioContainer.GetWidget("BackgroundMuted")).value;
        config.audio.inGameBackground.volume = (uint)((GenericOptionsInteger)audioContainer.GetWidget("InGameBackgroundVolume")).GetSelectedOption();
        config.audio.inGameBackground.muted = ((GenericBool)audioContainer.GetWidget("InGameBackgroundMuted")).value;

        configHelper.Save(isGlobal, config);
    }

    public void InsertCoin()
    {
        ControlEnable(true);
        status = StatusOptions.onBoot;
        scr.Clear();
        bootScreen.Reset();
    }

    public void mainMenuDraw()
    {
        scr.Clear();
        mainMenu.Deselect();
        mainMenu.DrawMenu();
        if (isGlobalConfigurationWidget.value)
        {
            scr.PrintCentered(4, "- global Configuration mode -");
            scr.PrintCentered(5, "(changes affects all rooms)");
        }
        else
        {
            scr.PrintCentered(4, "- room Configuration mode -");
        }
    }

    public void changeModeWindowDraw()
    {
        scr.Clear();
        if (configHelper.CanConfigureRoom())
        {
            isGlobalConfigurationWidget.enabled = true;
            scr.Print(2, 1, "select the configuration mode:");
            scr.Print(2, 2, "- global for all rooms");
            scr.Print(2, 3, "- or for this room only.");
            scr.Print(2, 4, "up/down to change");
        }
        else
        {
            isGlobalConfigurationWidget.enabled = false;
            scr.Print(2, 1, "only global configuration allowed");
            scr.Print(2, 3, "for one room configuration go to");
            scr.Print(2, 4, "a gallery room");
        }
        scr.Print(2, 19, "b to select");

        changeModeContainer.Draw();
    }

    public void controllerContainerDraw()
    {
        if (isGlobalConfigurationWidget.value)
        {
            lblGameSelected.label = "global configuration";
        }
        else
        {
            controlMapGameId.enabled = true;
            lblGameSelected.label = controlMapGameId.GetSelectedOption();
        }
        scr.Clear();
        scr.PrintCentered(1, "- Controller configuration -");
        controllerContainer.Draw();
        scr.Print(2, 23, "up/down/left/right to change");
        scr.Print(2, 24, "b to select");
    }

    private void controlMapConfigurationLoad()
    {
        try
        {
            if (isGlobalConfigurationWidget.value)
            {
                controlMapConfiguration = new GlobalControlMap();
            }
            else
            {
                controlMapConfiguration = new GameControlMap(lblGameSelected.label);
            }
        }
        catch (Exception e)
        {
            controlMapConfiguration = new DefaultControlMap();
            ConfigManager.WriteConsoleException($"[controllerLoadConfigMap] loading configuration, using default. Is Global:{isGlobalConfigurationWidget.value} ", e);
        }
        // ConfigManager.WriteConsole($"[controllerLoadConfigMap] debug in the next line ...");
        // controlMapConfiguration.ToDebug();
    }
    private void controlMapUpdateWidgets()
    {
        string mameControl = controlMapMameControl.GetSelectedOption();
        ConfigManager.WriteConsole($"[controlMapUpdateWidgets] updating for mame control id {mameControl}");

        //clean
        int idx = 0;
        for (; idx < 5; idx++)
        {
            controlMapRealControls[idx].SetCurrent("--");
        }

        ControlMapConfiguration.Maps maps = controlMapConfiguration.GetMap(mameControl);
        if (maps == null)
            return;

        //load
        idx = 0;
        foreach (ControlMapConfiguration.ControlMap m in maps.controlMaps)
        {
            controlMapRealControls[idx].SetCurrent(m.RealControl);
            idx++;
            if (idx > 4)
                break;
        }
    }

    private void controlMapUpdateConfigurationFromWidgets()
    {
        string mameControl = controlMapMameControl.GetSelectedOption();
        ConfigManager.WriteConsole($"[controlMapUpdateConfigurationFromWidgets] updating from widget mame control id {mameControl}");

        controlMapConfiguration.RemoveMaps(mameControl);
        for (int idx = 0; idx < 5; idx++)
        {
            string realControl = controlMapRealControls[idx].GetSelectedOption();
            if (realControl != "--")
            {
                controlMapConfiguration.AddMap(mameControl, realControl);
            }
        }

    }
    private bool controlMapConfigurationSave()
    {
        try
        {
            if (controlMapConfiguration is GlobalControlMap)
            {
                GlobalControlMap g = (GlobalControlMap)controlMapConfiguration;
                g.Save();
            }
            else if (controlMapConfiguration is GameControlMap)
            {
                GameControlMap g = (GameControlMap)controlMapConfiguration;
                g.Save();
            }
            else
            {
                ConfigManager.WriteConsoleError($"[controlMapConfigurationSave] Only can configure the Global or Game configuration controlls.");
            }
        }
        catch (Exception ex)
        {
            ConfigManager.WriteConsoleException($"[controlMapConfigurationSave] error saving configuration: {controlMapConfiguration}", ex);
            return false;
        }

        return true;
    }

    public void ScreenWaitingDraw()
    {
        scr.Clear();
        scr.PrintCentered(10, " - Wait for room setup - ");
        scr.PrintCentered(12, cabinetsController.Room, true);
    }


    private void SetMainMenuWidgets()
    {
        //main menu
        mainMenu = new(scr, "AGE of Joy - Main configuration");
        if (canChangeAudio)
            mainMenu.AddOption("Audio configuration", "    Change sound volume       ");
        if (canChangeNPC)
            mainMenu.AddOption("NPC configuration", "  To change the NPC behavior  ");
        if (canChangeControllers)
            mainMenu.AddOption("controllers", "Map your controls to play games");
        if (canChangeCabinets && configHelper.CanConfigureRoom())
            mainMenu.AddOption("cabinets", " replace cabinets in the room  ");
        mainMenu.AddOption("reset", "  global or room configuration ");
        mainMenu.AddOption("change mode", "         back to default       ");
        mainMenu.AddOption("exit", "        exit configuration     ");
    }

    private void SetAudioWidgets()
    {
        //audio
        audioContainer = new(scr, "audioContainer");
        audioContainer.Add(new GenericWindow(scr, 2, 4, "audiowin", 36, 14, " Audio Configuration "))
                      .Add(new GenericLabel(scr, "BackgroundLabel", "Background Audio", 4, 6))
                      .Add(new GenericBool(scr, "BackgroundMuted", "mute:", false, 6, 8))
                      .Add(new GenericOptionsInteger(scr, "BackgroundVolume", "volume:", 0, 100, 6, 9))
                      .Add(new GenericLabel(scr, "InGameBackgroundLabel", "Background in game audio", 4, 11))
                      .Add(new GenericBool(scr, "InGameBackgroundMuted", "mute:", false, 6, 13))
                      .Add(new GenericOptionsInteger(scr, "InGameBackgroundVolume", "volume:", 0, 100, 6, 14))
                      .Add(new GenericButton(scr, "save", "save & exit", 4, 16, true))
                      .Add(new GenericButton(scr, "exit", "exit", 18, 16, true))
                      .Add(new GenericLabel(scr, "l1", "left/right/b to change", 2, 20))
                      .Add(new GenericLabel(scr, "l2", "up/down to move", 2, 21));
    }

    private void SetChangeModeWidgets()
    {
        //change mode
        isGlobalConfigurationWidget = new GenericBool(scr, "isGlobal", "working with global:", !configHelper.CanConfigureRoom(), 4, 10);
        isGlobalConfigurationWidget.enabled = isGlobalConfigurationWidget.value;

        changeModeContainer = new(scr, "changeMode");
        changeModeContainer.Add(new GenericWindow(scr, 2, 8, "win", 36, 6, " mode "))
                           .Add(isGlobalConfigurationWidget)
                           .Add(new GenericButton(scr, "exit", "exit", 4, 11, true));
    }

    private void SetNPCWidgets()
    {
        //NPC configuration options.
        // take the statuses from the static value options in the information NPC class.
        npcWindow = new(scr, 2, 8, "npcWindow", 36, 6, " NPC Configuration ", true);
        NPCStatusOptions = new(scr, "npc", "NPC Behavior:", new List<string>(ConfigInformation.NPC.validStatus), 4, 10);

        //reset options
        resetContainer = new(scr, "reset");
        resetContainer.Add(new GenericWindow(scr, 2, 8, "win", 36, 6, " reset "))
                      .Add(new GenericButton(scr, "reset", "reset and save", 4, 11, true))
                      .Add(new GenericButton(scr, "exit", "exit", 4, 12, true));
    }

    private void SetControllersWidgets()
    {
        //controllers
        //Game selection
        // ---- title   : 4
        // | lblGame    : 6
        // |  lblCtlr   : 7
        // | option     : 9
        lblGameSelected = new GenericLabel(scr, "lblGame", "global configuration", 3, 6);
        controlMapGameId = new GenericOptions(scr, "gameId", "game:",
                                            cabinetsController?.gameRegistry?.GetCabinetsNamesAssignedToRoom(cabinetsController.Room), 3, 9);
        //global configuration by default, changed in the first draw()
        controlMapGameId.enabled = false;
        controlMapMameControl = new GenericOptions(scr, "mameControl", "CTRL:", LibretroMameCore.deviceIdsCombined, 3, 10);
        controlMapRealControls = new();
        List<string> controlMapRealControlList = new List<string>();
        controlMapRealControlList.Add("--");
        controlMapRealControlList = controlMapRealControlList.Concat(ControlMapPathDictionary.getList()).ToList();
        controlMapSavedLabel = new(scr, "saved", "saved", 3, 19, true);
        controllerContainer = new(scr, "controllers");
        controllerContainer.Add(new GenericWindow(scr, 1, 4, "controllerscont", 39, 18, " controllers "))
                           .Add(lblGameSelected)
                           .Add(controlMapGameId)
                           .Add(controlMapMameControl);
        for (int x = 0; x < 5; x++)
        {
            controlMapRealControls.Add(new GenericOptions(scr, "controlMapRealControl-" + x.ToString(), x.ToString() + ":", controlMapRealControlList, 3, 11 + x));
            controllerContainer.Add(controlMapRealControls[x]);
        }
        controllerContainer.Add(new GenericButton(scr, "save", "save", 3, 17, true))
                           .Add(new GenericButton(scr, "exit", "exit", 10, 17, true))
                           .Add(controlMapSavedLabel);
    }
    private void SetCabinetsWidgets()
    {
        if (! configHelper.CanConfigureRoom() || gameRegistry == null)
            return;

        // string roomName = roomConfiguration.GetName();
        GenericLabel lblRoomName = new GenericLabel(scr, "lblRoomName", cabinetsController.Room, 4, 6);
        cabinetToReplace = new GenericOptions(scr, "cabinetToReplace", "replace:", null, 4, 8, maxLength:26);
        cabinetToReplace.SetOptions(gameRegistry.GetCabinetsNamesAssignedToRoom(cabinetsController.Room));
        cabinetReplaced = new GenericOptions(scr, "cabinetReplaced", "with:", null, 4, 9, maxLength:26);
        cabinetReplaced.SetOptions(gameRegistry.GetAllCabinetsName());

        cabinetsToChangeContainer = new(scr, "cabinetsToChangeContainer");
        cabinetsToChangeContainer.Add(new GenericWindow(scr, 2, 4, "cabswin", 37, 12, " replace cabinets "))
                                .Add(lblRoomName)
                                .Add(cabinetToReplace)
                                .Add(cabinetReplaced)
                                .Add(new GenericButton(scr, "save", "save",  4, 11, true))
                                .Add(new GenericButton(scr, "exit", "exit",  4, 12, true));
                           
    }

    private void CabinetsWindowDraw()
    {
        if (cabinetsToChangeContainer == null)
            return;
        
        scr.Clear();
        cabinetsToChangeContainer.Draw();
        scr.Print(2, 23, "up/down/left/right to change");
        scr.Print(2, 24, "b to select");
    }

    IEnumerator run()
    {
        // first: wait for the room to load.
        configHelper = new(globalConfiguration, roomConfiguration);
        if (configHelper.CanConfigureRoom() && cabinetsController != null)
        {
            ScreenWaitingDraw();
            while (!cabinetsController.Loaded)
            {
                yield return new WaitForSeconds(1f / 2f);
                ScreenWaitingDraw();
            }
        }
        else
        {
            yield return new WaitForSeconds(1f / 2f);
        }

        if (cabinetsController?.gameRegistry == null)
            ConfigManager.WriteConsole("[ConfigurationController] gameregistry component not found, cant configure games controllers");

        //create widgets
        //boot
        bootScreen = new(scr);
        SetMainMenuWidgets();
        SetAudioWidgets();
        SetChangeModeWidgets();
        SetNPCWidgets();
        SetControllersWidgets();
        SetCabinetsWidgets();

        //load control map
        controlMapConfigurationLoad();
        controlMapUpdateWidgets();

        //main cycle
        status = StatusOptions.init;
        tree = buildBT();
        while (true)
        {
            tree.Tick();

            if (status == StatusOptions.init || status == StatusOptions.waitingForCoin)
                yield return new WaitForSeconds(1f);
            else if (status == StatusOptions.onBoot)
                yield return new WaitForSeconds(1f / 4f);
            else
                yield return new WaitForSeconds(1f / 6f);
        }
    }
    private BehaviorTree buildBT()
    {
        return new BehaviorTreeBuilder(gameObject)
          .Selector()

            .Sequence("Init")
              .Condition("On init", () => status == StatusOptions.init)
              .Do("Process", () =>
                {
                    ControlEnable(false);
                    status = StatusOptions.waitingForCoin;
                    scr.Clear()
                       .PrintCentered(10, "Insert coin to start", true)
                       .DrawScreen();
                    return TaskStatus.Success;
                })
            .End()

            .Sequence("Insert coin")
              .Condition("Waiting for coin", () => status == StatusOptions.waitingForCoin)
              .Condition("Is a coin in the bucket", () => (CoinSlot != null && CoinSlot.takeCoin()) || ControlActive("INSERT"))
              .Do("coin inserted", () =>
                {
                    InsertCoin();
                    return TaskStatus.Success;
                })
            .End()

            .Sequence("Boot")
              .Condition("Booting", () => status == StatusOptions.onBoot)
              .Condition("Finished lines", () =>
              {
                  bool finished = bootScreen.PrintNextLine();
                  scr.DrawScreen();
                  return finished;
              })
              .Do("Start main menu", () =>
                {
                    status = StatusOptions.onMainMenu;
                    return TaskStatus.Success;
                })
            .End()

            .Sequence("Main menu")
              .Condition("On main menu", () => status == StatusOptions.onMainMenu)
              .Do("Init", () =>
                {
                    scr.Clear();
                    mainMenuDraw();
                    scr.DrawScreen();
                    return TaskStatus.Success;
                })
              .Do("Process", () =>
                {
                    if (ControlActive("JOYPAD_UP") || ControlActive("KEYB-UP"))
                        mainMenu.PreviousOption();
                    else if (ControlActive("JOYPAD_DOWN") || ControlActive("KEYB-DOWN"))
                        mainMenu.NextOption();
                    else if (ControlActive("JOYPAD_B"))
                        mainMenu.Select();

                    if (!mainMenu.IsSelected())
                    {
                        scr.DrawScreen();
                        return TaskStatus.Continue;
                    }
                    ConfigManager.WriteConsole($"[ConfigurationController] option selected: {mainMenu.GetSelectedOption()}");
                    if (mainMenu.GetSelectedOption() == "NPC configuration")
                        status = StatusOptions.onNPCMenu;
                    else if (mainMenu.GetSelectedOption() == "exit")
                        status = StatusOptions.exit;
                    else if (mainMenu.GetSelectedOption() == "Audio configuration")
                        status = StatusOptions.onAudio;
                    else if (mainMenu.GetSelectedOption() == "change mode")
                        status = StatusOptions.onChangeMode;
                    else if (mainMenu.GetSelectedOption() == "reset")
                        status = StatusOptions.onReset;
                    else if (mainMenu.GetSelectedOption() == "cabinets")
                        status = StatusOptions.onChangeCabinets;
                    else if (mainMenu.GetSelectedOption() == "controllers")
                    {
                        status = StatusOptions.onChangeController;
                    }
                    mainMenu.Deselect();
                    scr.DrawScreen();
                    return TaskStatus.Success;
                })
            .End()

            .Sequence("NPC Configuration")
              .Condition("On NPC Config", () => status == StatusOptions.onNPCMenu)
              .Do("Init", () =>
                {
                    NPCScreen();
                    scr.DrawScreen();
                    return TaskStatus.Success;
                })
              .Do("Process", () =>
                {
                    if (ControlActive("JOYPAD_LEFT") || ControlActive("KEYB-LEFT"))
                        NPCStatusOptions.PreviousOption();
                    else if (ControlActive("JOYPAD_RIGHT") || ControlActive("KEYB-RIGHT"))
                        NPCStatusOptions.NextOption();
                    else if (ControlActive("JOYPAD_B"))
                    {
                        scr.DrawScreen();
                        return TaskStatus.Success;
                    }
                    scr.DrawScreen();
                    return TaskStatus.Continue;
                })
              .Do("Save", () =>
                {
                    NPCSave();
                    status = StatusOptions.onMainMenu;
                    return TaskStatus.Success;
                })
            .End()

            .Sequence("Audio Configuration")
              .Condition("On Config", () => status == StatusOptions.onAudio)
              .Do("Init", () =>
                {
                    audioScreen();
                    audioContainer.Draw();
                    scr.DrawScreen();
                    return TaskStatus.Success;
                })
              .Do("Process", () =>
                {
                    changeContainerSelection(audioContainer);
                    if (ControlActive("JOYPAD_B"))
                    {
                        GenericWidget w = audioContainer.GetSelectedWidget();
                        if (w != null)
                        {
                            if (w.name == "exit")
                            {
                                status = StatusOptions.onMainMenu;
                                return TaskStatus.Success;
                            }
                            else if (w.name == "save")
                            {
                                audioSave();
                                status = StatusOptions.onMainMenu;
                                return TaskStatus.Success;
                            }
                            w.Action();
                        }
                    }
                    scr.DrawScreen();
                    return TaskStatus.Continue;
                })
            .End()

            .Sequence("Change Mode")
              .Condition("On change mode", () => status == StatusOptions.onChangeMode)
              .Do("Init", () =>
                {
                    changeModeWindowDraw();
                    scr.DrawScreen();
                    return TaskStatus.Success;
                })
              .Do("Process", () =>
                {
                    changeContainerSelection(changeModeContainer);
                    GenericWidget w = changeModeContainer.GetSelectedWidget();
                    if (w != null && ControlActive("JOYPAD_B"))
                    {
                        if (w.name == "exit")
                        {
                            status = StatusOptions.onMainMenu;
                            return TaskStatus.Success;
                        }
                        w.Action();
                    }
                    scr.DrawScreen();
                    return TaskStatus.Continue;
                })
            .End()

            .Sequence("back to default")
              .Condition("On back to default", () => status == StatusOptions.onReset)
              .Do("Init", () =>
                {
                    resetWindowDraw();
                    scr.DrawScreen();
                    return TaskStatus.Success;
                })
              .Do("Process", () =>
                {
                    changeContainerSelection(resetContainer);
                    GenericWidget w = resetContainer.GetSelectedWidget();
                    if (w != null && ControlActive("JOYPAD_B"))
                    {
                        if (w.name == "exit")
                        {
                            status = StatusOptions.onMainMenu;
                            return TaskStatus.Success;
                        }
                        else if (w.name == "reset")
                        {
                            resetSave();
                            status = StatusOptions.onMainMenu;
                            return TaskStatus.Success;
                        }
                    }
                    scr.DrawScreen();
                    return TaskStatus.Continue;
                })
            .End()

            .Sequence("controller config")
              .Condition("On selecting game", () => status == StatusOptions.onChangeController)
              .Do("Init", () =>
                {
                    controllerContainerDraw();
                    scr.DrawScreen();

                    return TaskStatus.Success;
                })
              .Do("Process", () =>
                {
                    if (ControlActive("JOYPAD_UP") || ControlActive("KEYB-UP"))
                        controllerContainer.PreviousOption();
                    else if (ControlActive("JOYPAD_DOWN") || ControlActive("KEYB-DOWN"))
                        controllerContainer.NextOption();

                    GenericWidget w = controllerContainer.GetSelectedWidget();

                    if (w != null)
                    {
                        bool right = ControlActive("JOYPAD_LEFT") || ControlActive("KEYB-LEFT");
                        bool left = ControlActive("JOYPAD_RIGHT") || ControlActive("KEYB-RIGHT");

                        if (ControlActive("JOYPAD_B"))
                        {
                            if (w.name == "exit")
                            {
                                status = StatusOptions.onMainMenu;
                                return TaskStatus.Success;
                            }
                            else if (w.name == "save")
                            {
                                if (controlMapConfigurationSave())
                                    controlMapSavedLabel.label = "saved ok    ";
                                else
                                    controlMapSavedLabel.label = "error saving";
                                controlMapSavedLabel.SetSecondsAndDraw(2);

                                controlMapUpdateWidgets();
                                controllerContainerDraw();
                            }
                        }
                        else if (right || left)
                        {
                            if (left)
                                w.PreviousOption();
                            else if (right)
                                w.NextOption();

                            if (w.name == "gameId")
                            {
                                lblGameSelected.label = controlMapGameId.GetSelectedOption();
                                controlMapConfigurationLoad();
                            }
                            else if (w.name.StartsWith("controlMapRealControl")) // controlMapRealControl or mameControl
                            {
                                controlMapUpdateConfigurationFromWidgets();
                            }
                            controlMapUpdateWidgets();
                            controllerContainerDraw();
                        }
                    }

                    controlMapSavedLabel.Draw();
                    scr.DrawScreen();

                    return TaskStatus.Continue;
                })

            .End()


            .Sequence("Cabinets")
              .Condition("On cabinets replacement", () => status == StatusOptions.onChangeCabinets)
              .Do("Init", () =>
                {
                    CabinetsWindowDraw();
                    scr.DrawScreen();
                    return TaskStatus.Success;
                })
              .Do("Process", () =>
                {
                    changeContainerSelection(cabinetsToChangeContainer);
                    GenericWidget w = cabinetsToChangeContainer.GetSelectedWidget();
                    if (w != null && ControlActive("JOYPAD_B"))
                    {
                        if (w.name == "exit")
                        {
                            status = StatusOptions.onMainMenu;
                            return TaskStatus.Success;
                        }
                        else if (w.name == "reset")
                        {
                            resetSave();
                            status = StatusOptions.onMainMenu;
                            return TaskStatus.Success;
                        }
                    }
                    scr.DrawScreen();
                    return TaskStatus.Continue;
                })
            .End()


            .Sequence("EXIT")
              //.Condition("Exit button", () => ControlActive("EXIT"))
              .Condition("Exit", () => status == StatusOptions.exit)
              .Do("exit", () =>
              {
                  ConfigManager.WriteConsole($"[ConfigurationController] EXIT ");
                  status = StatusOptions.init;
                  return TaskStatus.Success;
              })
            .End()

          .End()
          .Build();
    }

    public void ControlEnable(bool enable)
    {
        changeControls.PlayerMode(enable);
        //if (inputActionManager != null)
        //    inputActionManager.enabled = !enable;
        if (enable)
        {
            actionMap.Enable();
            return;
        }
        actionMap.Disable();

        return;
    }

    public bool ControlActive(string mameControl)
    {
        bool ret = false;

        InputAction action = actionMap.FindAction(mameControl);
        if (action == null)
        {
            //ConfigManager.WriteConsoleError($"[ConfigurationControl.Active] [{mameControl}] not found in controlMap");
            return false;
        }

        //https://docs.unity3d.com/Packages/com.unity.inputsystem@1.5/api/UnityEngine.InputSystem.InputAction.html#UnityEngine_InputSystem_InputAction_WasPerformedThisFrame
        if (action.type == InputActionType.Button)
        {
            if (action.IsPressed())
            {
                return true;
            }
            return false;
        }

        else if (action.type == InputActionType.Value)
        {
            Vector2 val = action.ReadValue<Vector2>();
            switch (mameControl)
            {
                case "JOYPAD_UP":
                    if (val.y > 0.5)
                    {
                        return true;
                    }
                    break;
                case "JOYPAD_DOWN":
                    if (val.y < -0.5)
                    {
                        return true;
                    }
                    break;
                case "JOYPAD_RIGHT":
                    if (val.x > 0.5)
                    {
                        return true;
                    }
                    break;
                case "JOYPAD_LEFT":
                    if (val.x < -0.5)
                    {
                        return true;
                    }
                    break;
            }
        }
        return ret;
    }

    private void changeContainerSelection(GenericWidgetContainer gwc)
    {
        if (ControlActive("JOYPAD_UP") || ControlActive("KEYB-UP"))
            gwc.PreviousOption();
        else if (ControlActive("JOYPAD_DOWN") || ControlActive("KEYB-DOWN"))
            gwc.NextOption();
        else if (ControlActive("JOYPAD_LEFT") || ControlActive("KEYB-LEFT"))
            gwc.GetSelectedWidget()?.PreviousOption();
        else if (ControlActive("JOYPAD_RIGHT") || ControlActive("KEYB-RIGHT"))
            gwc.GetSelectedWidget()?.NextOption();
        return;
    }

}

#if UNITY_EDITOR
[CustomEditor(typeof(ConfigurationController))]
public class ConfigurationControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        ConfigurationController myScript = (ConfigurationController)target;
        if(GUILayout.Button("InsertCoin"))
        {
          myScript.InsertCoin();
        }
    }
}
#endif

