using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using KModkit;
using UnityEngine;
using UnityEngine.UI;

public class OrganizationScript : MonoBehaviour
{
    public KMAudio audio;
    public KMBombInfo bomb;

    public KMSelectable[] buttons;
    public GameObject[] otherObjs;
    public GameObject justSwitch;
    public GameObject justPencil;

    public static string[] fullModuleList = null;
    private string[] ignoredModules;
    private string[] backModules = new[] { "The Jewel Vault", "Turtle Robot", "Lightspeed", "Number Nimbleness", "3D Maze", "3D Tunnels", "Bomb Diffusal", "Kudosudoku", "Old Fogey", "Button Grid",
        "Reordered Keys", "Misordered Keys", "Recorded Keys", "Disordered Keys", "Simon Sings", "Vectors", "Game of Life Cruel", "Mastermind Cruel", "Factory Maze", "Simon Sends", "Quintuples",
        "The Hypercube", "The Ultracube", "Lombax Cubes", "Bamboozling Button", "Simon Stores", "The Cube", "The Sphere", "Ten-Button Color Code", "LEGOs", "Unfair Cipher", "Ultimate Cycle", "Ultimate Cipher", "Bamboozled Again" };

    private static readonly string[] ttksBefore = { "Morse Code", "Wires", "Two Bits", "The Button", "Colour Flash", "Round Keypad", "Password", "Who's On First", "Crazy Talk", "Keypad", "Listening", "Orientation Cube" };
    private static readonly string[] ttksAfter = { "Semaphore", "Combination Lock", "Simon Says", "Astrology", "Switches", "Plumbing", "Maze", "Memory", "Complicated Wires", "Wire Sequence", "Cryptography" };
    private List<string> order = new List<string>();
    private List<string> mysteryModuleHiddens = new List<string>();
    private List<string> mysteryModuleKeys = new List<string>();
    private List<string> solved = new List<string>();

    private string nextSwitch;
    private string currentSwitch;

    private bool cooldown = false;

    sealed class OrgBombInfo
    {
        public List<OrganizationScript> Modules = new List<OrganizationScript>();
    }

    private static readonly Dictionary<string, OrgBombInfo> _infos = new Dictionary<string, OrgBombInfo>();

    private OrgBombInfo info;

    public GameObject module;
    public GameObject arrow;

    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;

    private bool readyForInput = false;
    private bool detectionDone = false;
    private bool announcedOtherOrg = false;
    private bool announcedTTKsCustomKeys = false;
    private bool announcedAccessCodes = false;
    private bool announcedMysteryModule = false;
    private bool otherOrgs = false;
    private bool started = false;
    private bool delayed = false;
    private int orgCount = 0;

    private OrganizationSettings Settings = new OrganizationSettings();

    void Awake()
    {
        ModConfig<OrganizationSettings> modConfig = new ModConfig<OrganizationSettings>("OrganizationSettings");
        //Read from the settings file, or create one if one doesn't exist
        Settings = modConfig.Settings;
        //Update the settings file in case there was an error during read
        modConfig.Settings = Settings;
        //Figure out if module is running on a mission and requesting certain settings
        string missionDesc = KTMissionGetter.Mission.Description;
        if (missionDesc != null)
        {
            Regex regex = new Regex(@"\[Organization\] ((true|false), *){3}(true|false)");
            var match = regex.Match(missionDesc);
            if (match.Success)
            {
                string[] options = match.Value.Replace("[Organization] ", "").Split(',');
                bool[] values = new bool[options.Length];
                for (int i = 0; i < options.Length; i++)
                    values[i] = options[i] == "true" ? true : false;
                Settings.ignoreSolveBased = values[0];
                Settings.enableMoveToBack = values[1];
                Settings.disableTimeModeCooldown = values[2];
                Settings.useSwitchVersion = values[3];
            }
        }
        nextSwitch = "";
        currentSwitch = "Up";
        moduleId = moduleIdCounter++;
        moduleSolved = false;
        foreach (KMSelectable obj in buttons)
        {
            KMSelectable pressed = obj;
            pressed.OnInteract += delegate () { PressButton(pressed); return false; };
        }
        if (fullModuleList == null)
        {
            fullModuleList = GetComponent<KMBossModule>().GetIgnoredModules("Organization", new string[]{
                "Forget Me Not",     //Mandatory to prevent unsolvable bombs.
				"Forget Everything", //Cruel FMN.
				"Turn The Key",      //TTK is timer based, and stalls the bomb if only it and FI are left.
				"Souvenir",          //Similar situation to TTK, stalls the bomb.
				"The Time Keeper",   //Again, timilar to TTK.
				"Forget This",
                "Simon's Stages",
                "Timing is Everything",
                "Organization", //Also mandatory to prevent unsolvable bombs.
                "The Swan",
                "Hogwarts",
                "Divided Squares",
                "Cookie Jars",
                "Turn The Keys",
                "Forget Them All",
                "Tallordered Keys",
                "Purgatory",
                "Forget Us Not",
                "Forget Perspective"
            });
        }
        List<IEnumerable<string>> ignoreLists = new List<IEnumerable<string>>();
        ignoredModules = fullModuleList.ToArray();
        //Split the lists at empty values
        while (ignoredModules.Length > 0)
        {
            ignoreLists.Add(ignoredModules.TakeWhile(x => x != ""));
            ignoredModules = ignoredModules.SkipWhile(x => x != "").Skip(1).ToArray();
        }
        //If we're ignoring solved based modules and the ignored module list is compatble, combine two of the lists
        if (Settings.ignoreSolveBased && ignoreLists.Count > 1)
            ignoredModules = ignoreLists[0].Concat(ignoreLists[1]).ToArray();
        //If the ignore module list is incompatible or solved based modules will not be ignored, either use the whole list or the first split
        else
            ignoredModules = ignoreLists.FirstOrDefault().ToArray();
        //If the JSON is compatible with move to back modules, add them here
        if (ignoreLists.Count > 2)
            backModules = ignoreLists.Last().ToArray();
        GetComponent<KMBombModule>().OnActivate += OnActivate;
    }

    void Start()
    {
        if (!Settings.useSwitchVersion)
        {
            justSwitch.SetActive(false);
            for (int i = 0; i < otherObjs.Length; i++)
                otherObjs[i].SetActive(false);
            justPencil.transform.localPosition = new Vector3(0.042f, 0.0045f, -0.03f);
            GetComponent<KMSelectable>().Children = new KMSelectable[] { buttons[1] };
            GetComponent<KMSelectable>().UpdateChildren();
        }
        else
            TwitchHelpMessage = @"!{0} continue/cont [Presses the continue button] | !{0} toggle/switch [Toggles the switch to move to the other positon (positions are either up or down)]";
    }

    public void MysteryModuleNotification(List<string> keys, string hidden)
    {
        Debug.LogFormat("[Organization #{0}] Mystery Module notified me that it is hiding '{1}' and its keys are '{2}'.", moduleId, hidden, keys.Join(", "));
        mysteryModuleHiddens.Add(hidden);
        mysteryModuleKeys.AddRange(keys);
    }

    void OnActivate()
    {
        arrow.GetComponent<Renderer>().enabled = false;
        var serialNumber = bomb.GetSerialNumber();
        if (!_infos.ContainsKey(serialNumber))
            _infos[serialNumber] = new OrgBombInfo();
        info = _infos[serialNumber];
        info.Modules.Add(this);
        if (Settings.disableTimeModeCooldown == true)
        {
            TimeModeActive = false;
        }
        Debug.LogFormat("[Organization #{0}] Time Mode Cooldown Active: '{1}'", moduleId, TimeModeActive);
        Debug.LogFormat("[Organization #{0}] Switch Version Active: '{1}'", moduleId, Settings.useSwitchVersion);
        generateOrder();
        if (bomb.GetSolvableModuleNames().Where(x => !ignoredModules.Contains(x)).Count() == 0)
        {
            if (Settings.useSwitchVersion)
                getNewSwitchPos();
            else
            {
                Debug.LogFormat("[Organization #{0}] No non-ignored modules detected! Autosolving!", moduleId);
                bomb.GetComponent<KMBombModule>().HandlePass();
                moduleSolved = true;
            }
        }
        if (Settings.useSwitchVersion)
            started = true;
    }

    void Update()
    {
        if (order.Count == 0 && detectionDone == false && started == true && Settings.useSwitchVersion)
        {
            detectionDone = true;
            readyForInput = true;
        }
    }

    int ticker = 0;
    void FixedUpdate()
    {
        if (moduleSolved != true)
        {
            ticker++;
            if (ticker == 20)
            {
                ticker = 0;
                int progress = bomb.GetSolvedModuleNames().Count();
                if (progress > solved.Count)
                {
                    if (Settings.useSwitchVersion)
                        Debug.LogFormat("[Organization #{0}] ---------------------------------------------------", moduleId);
                    string name = getLatestSolve(bomb.GetSolvedModuleNames(), solved);
                    if (ignoredModules.Contains(name))
                    {
                        Debug.LogFormat("[Organization #{0}] Ignored module: '{1}' has been solved", moduleId, name);
                        solved.Add(name);
                        //Switch check for ignored module solve
                        if (Settings.useSwitchVersion)
                            getNewSwitchPos();
                    }
                    else if (cooldown == true || TwitchAbandonModule.Any(module => module.ModuleDisplayName.Equals(name)))
                    {
                        if (cooldown)
                            Debug.LogFormat("[Organization #{0}] '{1}' has been solved, but due to timemode's cooldown the strike will be ignored! Removing from future possibilities...", moduleId, name);
                        else
                            Debug.LogFormat("[Organization #{0}] '{1}' has been solved, but due to being force solved the strike will be ignored! Removing from future possibilities...", moduleId, name);
                        solved.Add(name);
                        order.Remove(name);
                        string build;
                        if (order.Count != 0)
                        {
                            build = "[Organization #{0}] The new order of the non-ignored modules has now been determined as: ";
                        }
                        else
                        {
                            build = "[Organization #{0}] The new order of the non-ignored modules has now been determined as: none";
                        }
                        for (int i = 0; i < order.Count; i++)
                        {
                            if (i == (order.Count - 1))
                            {
                                build += order[i];
                            }
                            else
                            {
                                build += (order[i] + ", ");
                            }
                        }
                        Debug.LogFormat(build, moduleId);
                        if (!cooldown)
                        {
                            if (Settings.useSwitchVersion)
                            {
                                if (module.GetComponent<Text>().text.Equals(solved.Last()) && !readyForInput)
                                {
                                    readyForInput = true;
                                    getNewSwitchPos();
                                }
                            }
                            else if (order.Count() == 0)
                            {
                                module.GetComponent<Text>().text = "No Modules :)";
                                Debug.LogFormat("[Organization #{0}] All non-ignored modules solved! GG!", moduleId);
                                moduleSolved = true;
                                bomb.GetComponent<KMBombModule>().HandlePass();
                            }
                            else if (module.GetComponent<Text>().text.Equals(solved.Last()))
                            {
                                Debug.LogFormat("[Organization #{0}] The next module is now shown! '{1}'!", moduleId, order[0]);
                                string temp = order[0];
                                if (temp.Contains('’'))
                                {
                                    temp = temp.Replace("’", "\'");
                                }
                                else if (temp.Contains('³'))
                                {
                                    temp = temp.Replace('³', '3');
                                }
                                else if (temp.Contains('è'))
                                {
                                    temp = temp.Replace('è', 'e');
                                }
                                else if (temp.Contains('ñ'))
                                {
                                    temp = temp.Replace('ñ', 'n');
                                }
                                module.GetComponent<Text>().text = "" + temp;
                            }
                        }
                    }
                    else
                    {
                        if (otherOrgs == true)
                        {
                            if (Settings.useSwitchVersion && readyForInput == true)
                            {
                                Debug.LogFormat("[Organization #{0}] '{1}' has been solved and this Organization needs to be manually given the next module in order to continue! Strike! Removing from future possibilities...", moduleId, name);
                                bomb.GetComponent<KMBombModule>().HandleStrike();
                                audio.PlaySoundAtTransform("order", transform);
                                solved.Add(name);
                                order.Remove(name);
                                string build;
                                if (order.Count != 0)
                                {
                                    build = "[Organization #{0}] The new order of the non-ignored modules has now been determined as: ";
                                }
                                else
                                {
                                    build = "[Organization #{0}] The new order of the non-ignored modules has now been determined as: none";
                                }
                                for (int i = 0; i < order.Count; i++)
                                {
                                    if (i == (order.Count - 1))
                                    {
                                        build += order[i];
                                    }
                                    else
                                    {
                                        build += (order[i] + ", ");
                                    }
                                }
                                Debug.LogFormat(build, moduleId);
                                getNewSwitchPos();
                            }
                            else
                            {
                                List<string> displayed = new List<string>();
                                List<bool> ready = new List<bool>();
                                List<string> nrdisplayed = new List<string>();
                                string tmpname = name;
                                tmpname = tmpname.Replace("’", "\'");
                                tmpname = tmpname.Replace('³', '3');
                                tmpname = tmpname.Replace('è', 'e');
                                tmpname = tmpname.Replace('ñ', 'n');
                                foreach (OrganizationScript mod in info.Modules)
                                {
                                    string tempname = mod.module.GetComponent<Text>().text;
                                    displayed.Add(tempname);
                                    if (Settings.useSwitchVersion)
                                        ready.Add(mod.readyForInput);
                                }
                                for (int i = 0; i < displayed.Count; i++)
                                {
                                    if (Settings.useSwitchVersion)
                                    {
                                        if (ready[i] == false)
                                            nrdisplayed.Add(displayed[i]);
                                    }
                                    else
                                    {
                                        nrdisplayed.Add(displayed[i]);
                                    }
                                }
                                if (nrdisplayed.Contains(tmpname))
                                {
                                    if (name.Equals(order[0]))
                                    {
                                        solved.Add(order[0]);
                                        order.RemoveAt(0);
                                        if (Settings.useSwitchVersion)
                                        {
                                            StartCoroutine(readyUpDelayed());
                                            Debug.LogFormat("[Organization #{0}] '{1}' has been solved for this Organization! Ready for next module...", moduleId, name);
                                            getNewSwitchPos();
                                        }
                                        else
                                        {
                                            Debug.LogFormat("[Organization #{0}] '{1}' has been solved for this Organization!", moduleId, name);
                                            StartCoroutine(slightDelay());
                                        }
                                    }
                                    else
                                    {
                                        Debug.LogFormat("[Organization #{0}] '{1}' has been solved and it was the next module on a different Organization! Removing from future possibilities on this Organization...", moduleId, name);
                                        solved.Add(name);
                                        order.Remove(name);
                                        string build;
                                        if (order.Count != 0)
                                        {
                                            build = "[Organization #{0}] The new order of the non-ignored modules has now been determined as: ";
                                        }
                                        else
                                        {
                                            build = "[Organization #{0}] The new order of the non-ignored modules has now been determined as: none";
                                        }
                                        for (int i = 0; i < order.Count; i++)
                                        {
                                            if (i == (order.Count - 1))
                                            {
                                                build += order[i];
                                            }
                                            else
                                            {
                                                build += (order[i] + ", ");
                                            }
                                        }
                                        Debug.LogFormat(build, moduleId);
                                        if (Settings.useSwitchVersion)
                                            getNewSwitchPos();
                                    }
                                }
                                else
                                {
                                    Debug.LogFormat("[Organization #{0}] '{1}' has been solved and it was not the next module on ANY Organization! Strike! Removing from future possibilities...", moduleId, name);
                                    bomb.GetComponent<KMBombModule>().HandleStrike();
                                    audio.PlaySoundAtTransform("order", transform);
                                    solved.Add(name);
                                    order.Remove(name);
                                    string build;
                                    if (order.Count != 0)
                                    {
                                        build = "[Organization #{0}] The new order of the non-ignored modules has now been determined as: ";
                                    }
                                    else
                                    {
                                        build = "[Organization #{0}] The new order of the non-ignored modules has now been determined as: none";
                                    }
                                    for (int i = 0; i < order.Count; i++)
                                    {
                                        if (i == (order.Count - 1))
                                        {
                                            build += order[i];
                                        }
                                        else
                                        {
                                            build += (order[i] + ", ");
                                        }
                                    }
                                    Debug.LogFormat(build, moduleId);
                                    if (Settings.useSwitchVersion)
                                        getNewSwitchPos();
                                }
                            }
                        }
                        else
                        {
                            if ((!Settings.useSwitchVersion && name.Equals(order[0])) || (Settings.useSwitchVersion && name.Equals(order[0]) && readyForInput == false))
                            {
                                solved.Add(order[0]);
                                order.RemoveAt(0);
                                if (Settings.useSwitchVersion)
                                {
                                    readyForInput = true;
                                    Debug.LogFormat("[Organization #{0}] '{1}' has been solved! Ready for next module...", moduleId, name);
                                    getNewSwitchPos();
                                }
                                else
                                {
                                    Debug.LogFormat("[Organization #{0}] '{1}' has been solved!", moduleId, name);
                                    if (order.Count() == 0)
                                    {
                                        module.GetComponent<Text>().text = "No Modules :)";
                                        Debug.LogFormat("[Organization #{0}] All non-ignored modules solved! GG!", moduleId);
                                        moduleSolved = true;
                                        bomb.GetComponent<KMBombModule>().HandlePass();
                                    }
                                    else
                                    {
                                        if (TimeModeActive == true)
                                        {
                                            if (otherOrgs == true)
                                            {
                                                module.GetComponent<Text>().text = "In Cooldown...";
                                            }
                                            else
                                            {
                                                module.GetComponent<Text>().text = "In Cooldown...";
                                            }
                                            StartCoroutine(timer());
                                        }
                                        else
                                        {
                                            Debug.LogFormat("[Organization #{0}] The next module is now shown! '{1}'!", moduleId, order[0]);
                                            string temp = order[0];
                                            if (temp.Contains('’'))
                                            {
                                                temp = temp.Replace("’", "\'");
                                            }
                                            else if (temp.Contains('³'))
                                            {
                                                temp = temp.Replace('³', '3');
                                            }
                                            else if (temp.Contains('è'))
                                            {
                                                temp = temp.Replace('è', 'e');
                                            }
                                            else if (temp.Contains('ñ'))
                                            {
                                                temp = temp.Replace('ñ', 'n');
                                            }
                                            module.GetComponent<Text>().text = "" + temp;
                                        }
                                    }
                                }
                            }
                            else if (Settings.useSwitchVersion && readyForInput == true)
                            {
                                Debug.LogFormat("[Organization #{0}] '{1}' has been solved and the module needs to be manually given the next module in order to continue! Strike! Removing from future possibilities...", moduleId, name);
                                bomb.GetComponent<KMBombModule>().HandleStrike();
                                audio.PlaySoundAtTransform("order", transform);
                                solved.Add(name);
                                order.Remove(name);
                                string build;
                                if (order.Count != 0)
                                {
                                    build = "[Organization #{0}] The new order of the non-ignored modules has now been determined as: ";
                                }
                                else
                                {
                                    build = "[Organization #{0}] The new order of the non-ignored modules has now been determined as: none";
                                }
                                for (int i = 0; i < order.Count; i++)
                                {
                                    if (i == (order.Count - 1))
                                    {
                                        build += order[i];
                                    }
                                    else
                                    {
                                        build += (order[i] + ", ");
                                    }
                                }
                                Debug.LogFormat(build, moduleId);
                                getNewSwitchPos();
                            }
                            else
                            {
                                Debug.LogFormat("[Organization #{0}] '{1}' has been solved and it was not the next module! Strike! Removing from future possibilities...", moduleId, name);
                                bomb.GetComponent<KMBombModule>().HandleStrike();
                                audio.PlaySoundAtTransform("order", transform);
                                solved.Add(name);
                                order.Remove(name);
                                string build;
                                if (order.Count != 0)
                                {
                                    build = "[Organization #{0}] The new order of the non-ignored modules has now been determined as: ";
                                }
                                else
                                {
                                    build = "[Organization #{0}] The new order of the non-ignored modules has now been determined as: none";
                                }
                                for (int i = 0; i < order.Count; i++)
                                {
                                    if (i == (order.Count - 1))
                                    {
                                        build += order[i];
                                    }
                                    else
                                    {
                                        build += (order[i] + ", ");
                                    }
                                }
                                Debug.LogFormat(build, moduleId);
                                if (Settings.useSwitchVersion)
                                    getNewSwitchPos();
                            }
                        }
                    }
                }
            }
        }
    }

    void PressButton(KMSelectable pressed)
    {
        if (moduleSolved != true && cooldown != true && delayed != true && Settings.useSwitchVersion)
        {
            audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, pressed.transform);
            pressed.AddInteractionPunch(0.25f);
            if (buttons[0] == pressed)
            {
                if (readyForInput == true)
                {
                    if (currentSwitch.Equals(nextSwitch))
                    {
                        readyForInput = false;
                        if (otherOrgs == true)
                        {
                            arrow.GetComponent<Renderer>().enabled = false;
                        }
                        if (order.Count == 0)
                        {
                            module.GetComponent<Text>().text = "No Modules :)";
                            Debug.LogFormat("[Organization #{0}] All non-ignored modules solved! GG!", moduleId);
                            moduleSolved = true;
                            bomb.GetComponent<KMBombModule>().HandlePass();
                        }
                        else
                        {
                            if (TimeModeActive == true)
                            {
                                audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);
                                if (otherOrgs == true)
                                {
                                    module.GetComponent<Text>().text = "In Cooldown...";
                                }
                                else
                                {
                                    module.GetComponent<Text>().text = "In Cooldown...";
                                }
                                StartCoroutine(timer());
                            }
                            else
                            {
                                audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);
                                Debug.LogFormat("[Organization #{0}] The next module is now shown! '{1}'!", moduleId, order[0]);
                                string temp = order[0];
                                if (temp.Contains('’'))
                                {
                                    temp = temp.Replace("’", "\'");
                                }
                                else if (temp.Contains('³'))
                                {
                                    temp = temp.Replace('³', '3');
                                }
                                else if (temp.Contains('è'))
                                {
                                    temp = temp.Replace('è', 'e');
                                }
                                else if (temp.Contains('ñ'))
                                {
                                    temp = temp.Replace('ñ', 'n');
                                }
                                module.GetComponent<Text>().text = "" + temp;
                            }
                        }
                    }
                    else
                    {
                        Debug.LogFormat("[Organization #{0}] The switch is not in the correct position (currently '{1}')! Strike!", moduleId, currentSwitch);
                        bomb.GetComponent<KMBombModule>().HandleStrike();
                        int rand = UnityEngine.Random.Range(0, 3);
                        if (rand == 0)
                        {
                            audio.PlaySoundAtTransform("wrong1", transform);
                        }
                        else if (rand == 1)
                        {
                            audio.PlaySoundAtTransform("wrong2", transform);
                        }
                        else
                        {
                            audio.PlaySoundAtTransform("wrong3", transform);
                        }
                    }
                }
                else
                {
                    Debug.LogFormat("[Organization #{0}] The current module has not been solved yet! Strike!", moduleId);
                    bomb.GetComponent<KMBombModule>().HandleStrike();
                    int rand = UnityEngine.Random.Range(0, 3);
                    if (rand == 0)
                    {
                        audio.PlaySoundAtTransform("wrong1", transform);
                    }
                    else if (rand == 1)
                    {
                        audio.PlaySoundAtTransform("wrong2", transform);
                    }
                    else
                    {
                        audio.PlaySoundAtTransform("wrong3", transform);
                    }
                }
            }
            else if (buttons[1] == pressed)
            {
                if (currentSwitch.Equals("Up"))
                {
                    currentSwitch = "Down";
                    StartCoroutine(downSwitch());
                }
                else if (currentSwitch.Equals("Down"))
                {
                    currentSwitch = "Up";
                    StartCoroutine(upSwitch());
                }
            }
        }
    }

    private void generateOrder()
    {
        Debug.LogFormat("[Organization #{0}] Organization process started!", moduleId);
        bool ttks = false;
        bool mm = false;
        bool access = false;
        int accessCt = 0;
        order = bomb.GetSolvableModuleNames();

        List<string> remove = new List<string>();
        List<string> moveToEnd = new List<string>();

        for (int i = 0; i < order.Count; i++)
        {
            if (ignoredModules.Contains(order[i]))
            {
                Debug.LogFormat("[Organization #{0}] Ignored module: '{1}' detected! Removing from possibilities...", moduleId, order[i]);
                remove.Add(order[i]);
            }
            if (order[i].Equals("Turn The Keys") || order[i].Equals("Custom Keys"))
            {
                if (announcedTTKsCustomKeys == false)
                {
                    ttks = true;
                    Debug.LogFormat("[Organization #{0}] TTKS/Custom Keys detected! Keeping this in mind for Organization process!", moduleId);
                    announcedTTKsCustomKeys = true;
                }
            }
            if (order[i].Equals("Mystery Module"))
            {
                if (announcedMysteryModule == false)
                {
                    mm = true;
                    Debug.LogFormat("[Organization #{0}] Mystery Module detected! Keeping this in mind for Organization process!", moduleId);
                    announcedMysteryModule = true;
                }
            }
            if (order[i].Equals("Organization"))
            {
                orgCount++;
                if (orgCount > 1 && announcedOtherOrg == false)
                {
                    otherOrgs = true;
                    Debug.LogFormat("[Organization #{0}] Other Organizations detected! Making sure to only strike for what is not on display on ANY of the Organizations!", moduleId);
                    announcedOtherOrg = true;
                }
            }
            if (order[i].Equals("Access Codes"))
            {
                if (announcedAccessCodes == false)
                {
                    access = true;
                    Debug.LogFormat("[Organization #{0}] Access Codes detected! Keeping this in mind for Organization process!", moduleId);
                    announcedAccessCodes = true;
                }
                accessCt++;
                remove.Add(order[i]);
            }
        }
        for (int j = 0; j < remove.Count; j++)
            order.Remove(remove[j]);
        order.Shuffle();

        //Makes sure TTKs/Custom Keys/Mystery Module will not softlock if this module is present
        if (ttks || mm)
        {
            List<string> befores = new List<string>();
            int mms = 0;
            List<string> afters = new List<string>();
            for (int i = 0; i < order.Count; i++)
            {
                if (mm && order[i] == "Mystery Module")
                    mms++;
                else if ((ttks && ttksBefore.Contains(order[i])) || (mm && mysteryModuleKeys.Contains(order[i])))
                    befores.Add(order[i]);
                else if ((ttks && ttksAfter.Contains(order[i])) || (mm && mysteryModuleHiddens.Contains(order[i])))
                    afters.Add(order[i]);
                else
                {
                    int rando = Random.Range(0, 2);
                    if (rando == 0)
                        befores.Add(order[i]);
                    else
                        afters.Add(order[i]);
                }
            }
            befores = befores.Shuffle();
            afters = afters.Shuffle();
            order = befores;
            for (var i = 0; i < mms; i++)
                order.Add("Mystery Module");
            order.AddRange(afters);
        }

        //Moves all Access Codes modules to the front
        if (access)
        {
            for (int i = 0; i < accessCt; i++)
                order.Insert(0, "Access Codes");
        }

        //Moves backModules to the end of order list in random order
        backModules = backModules.Shuffle();
        if (Settings.enableMoveToBack)
        {
            for (int i = 0; i < backModules.Count(); i++)
            {
                int backCount = 0;
                for (int j = 0; j < order.Count; j++)
                    if (order[j].Equals(backModules[i]))
                        backCount++;
                for (int j = 0; j < backCount; j++)
                    order.Remove(backModules[i]);
                for (int j = 0; j < backCount; j++)
                    order.Add(backModules[i]);
            }
        }

        if (order.Count != 0)
        {
            string temp = order[0]
                .Replace("’", "\'")
                .Replace('³', '3')
                .Replace('è', 'e')
                .Replace('ñ', 'n');
            module.GetComponent<Text>().text = "" + temp;
            Debug.LogFormat("[Organization #{0}] The order of the non-ignored modules has been determined as: {1}", moduleId, order.Join(", "));
        }
        else
        {
            Debug.LogFormat("[Organization #{0}] The order of the non-ignored modules has been determined as: none", moduleId);
            module.GetComponent<Text>().text = "No Modules :)";
        }
    }

    private IEnumerator readyUpDelayed()
    {
        delayed = true;
        yield return new WaitForSeconds(1.0f);
        readyForInput = true;
        arrow.GetComponent<Renderer>().enabled = true;
        delayed = false;
        StopCoroutine("readyUpDelayed");
    }

    private string getLatestSolve(List<string> s, List<string> s2)
    {
        for (int i = 0; i < s2.Count; i++)
            s.Remove(s2[i]);
        return s[0];
    }

    private void getNewSwitchPos()
    {
        Debug.LogFormat("[Organization #{0}] Checking Switch Position Rules...", moduleId);
        if (solved.Count == 0)
        {
            nextSwitch = "Up";
            Debug.LogFormat("[Organization #{0}] Switch Table Rule 1 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
        }
        else if (solved.Count == 1)
        {
            if (vowelCount(module.GetComponent<Text>().text.Replace('è', 'e')) % 2 == 0)
            {
                nextSwitch = "Down";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 2 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (solved[solved.Count - 1].Equals("The Digit") || solved[solved.Count - 1].Equals("Mega Man 2") || solved[solved.Count - 1].Equals("Unfair Cipher"))
            {
                nextSwitch = "Up";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 3 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (module.GetComponent<Text>().text.StartsWith("S"))
            {
                nextSwitch = "Down";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 4 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (bomb.IsIndicatorPresent("SND"))
            {
                nextSwitch = "Up";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 5 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (bomb.GetModuleNames().Contains("Forget This"))
            {
                nextSwitch = "Down";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 6 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (bomb.GetBatteryCount() <= 2)
            {
                nextSwitch = "Up";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 7 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else
            {
                nextSwitch = "Down";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 8 (no others true rule) is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
        }
        else if (solved.Count == 2)
        {
            if (vowelCount(module.GetComponent<Text>().text) % 2 == 0)
            {
                nextSwitch = "Down";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 2 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (solved[solved.Count - 1].Equals("The Digit") || solved[solved.Count - 1].Equals("Mega Man 2") || solved[solved.Count - 1].Equals("Unfair Cipher"))
            {
                nextSwitch = "Up";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 3 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (module.GetComponent<Text>().text.StartsWith("S"))
            {
                nextSwitch = "Down";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 4 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (bomb.IsIndicatorPresent("SND"))
            {
                nextSwitch = "Up";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 5 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (bomb.GetModuleNames().Contains("Forget This"))
            {
                nextSwitch = "Down";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 6 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (bomb.GetBatteryCount() <= 2)
            {
                nextSwitch = "Up";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 7 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else
            {
                nextSwitch = "Down";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 8 (no others true rule) is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
        }
        else if (solved.Count == 3)
        {
            nextSwitch = "Up";
            Debug.LogFormat("[Organization #{0}] Switch Table Rule 1 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
        }
        else if (solved.Count == 4)
        {
            if (vowelCount(module.GetComponent<Text>().text) % 2 == 0)
            {
                nextSwitch = "Down";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 2 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (solved[solved.Count - 1].Equals("The Digit") || solved[solved.Count - 1].Equals("Mega Man 2") || solved[solved.Count - 1].Equals("Unfair Cipher"))
            {
                nextSwitch = "Up";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 3 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (module.GetComponent<Text>().text.StartsWith("S"))
            {
                nextSwitch = "Down";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 4 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (bomb.IsIndicatorPresent("SND"))
            {
                nextSwitch = "Up";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 5 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (bomb.GetModuleNames().Contains("Forget This"))
            {
                nextSwitch = "Down";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 6 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (bomb.GetBatteryCount() <= 2)
            {
                nextSwitch = "Up";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 7 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else
            {
                nextSwitch = "Down";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 8 (no others true rule) is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
        }
        else
        {
            if (((bomb.GetSolvedModuleNames().Count % 3) == 0))
            {
                nextSwitch = "Up";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 1 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (vowelCount(module.GetComponent<Text>().text) % 2 == 0)
            {
                nextSwitch = "Down";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 2 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (solved[solved.Count - 1].Equals("The Digit") || solved[solved.Count - 1].Equals("Mega Man 2") || solved[solved.Count - 1].Equals("Unfair Cipher"))
            {
                nextSwitch = "Up";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 3 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (module.GetComponent<Text>().text.StartsWith("S"))
            {
                nextSwitch = "Down";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 4 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (bomb.IsIndicatorPresent("SND"))
            {
                nextSwitch = "Up";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 5 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (bomb.GetModuleNames().Contains("Forget This"))
            {
                nextSwitch = "Down";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 6 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (bomb.GetBatteryCount() <= 2)
            {
                nextSwitch = "Up";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 7 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else
            {
                nextSwitch = "Down";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 8 (no others true rule) is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
        }
    }

    private int vowelCount(string s)
    {
        int vowcount = 0;
        char[] vowels = { 'A', 'E', 'O', 'I', 'U' };
        s = s.ToUpperInvariant();
        for (int i = 0; i < s.Length; i++)
            if (vowels.Contains(s[i]))
                vowcount++;
        return vowcount;
    }

    private IEnumerator downSwitch()
    {
        int movement = 0;
        while (movement != 10)
        {
            yield return new WaitForSeconds(0.0001f);
            buttons[1].transform.localPosition = buttons[1].transform.localPosition + Vector3.forward * -0.00225f;
            movement++;
        }
        StopCoroutine("downSwitch");
    }

    private IEnumerator upSwitch()
    {
        int movement = 0;
        while (movement != 10)
        {
            yield return new WaitForSeconds(0.0001f);
            buttons[1].transform.localPosition = buttons[1].transform.localPosition + Vector3.back * -0.00225f;
            movement++;
        }
        StopCoroutine("upSwitch");
    }

    private IEnumerator timer()
    {
        cooldown = true;
        double counter = UnityEngine.Random.Range(30, 46);
        if (otherOrgs == true)
        {
            Debug.LogFormat("[Organization #{0}] Cooldown activated! There is {1} seconds of free solving until the next module is shown!", moduleId, (int) counter);
        }
        else
        {
            Debug.LogFormat("[Organization #{0}] Cooldown activated! There is {1} seconds until the next module is shown! (No guaranteed free solves due to other orgs)", moduleId, (int) counter);
        }
        while (counter > 0)
        {
            yield return new WaitForSeconds(0.1f);
            if (counter < 11)
            {
                module.GetComponent<Text>().text = "" + (int) counter;
            }
            counter -= 0.1;
        }
        cooldown = false;
        if (order.Count() == 0)
        {
            module.GetComponent<Text>().text = "No Modules :)";
            Debug.LogFormat("[Organization #{0}] All non-ignored modules solved! GG!", moduleId);
            moduleSolved = true;
            bomb.GetComponent<KMBombModule>().HandlePass();
            yield break;
        }
        audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.NeedyActivated, transform);
        Debug.LogFormat("[Organization #{0}] Cooldown over! The next module is now shown! '{1}'!", moduleId, order[0]);
        string temp = order[0];
        if (temp.Contains('’'))
        {
            temp = temp.Replace("’", "\'");
        }
        else if (temp.Contains('³'))
        {
            temp = temp.Replace('³', '3');
        }
        else if (temp.Contains('è'))
        {
            temp = temp.Replace('è', 'e');
        }
        else if (temp.Contains('ñ'))
        {
            temp = temp.Replace('ñ', 'n');
        }
        module.GetComponent<Text>().text = "" + temp;
        StopCoroutine("timer");
    }

    private IEnumerator slightDelay()
    {
        yield return null;
        if (order.Count() == 0)
        {
            module.GetComponent<Text>().text = "No Modules :)";
            Debug.LogFormat("[Organization #{0}] All non-ignored modules solved! GG!", moduleId);
            moduleSolved = true;
            bomb.GetComponent<KMBombModule>().HandlePass();
        }
        else
        {
            if (TimeModeActive == true)
            {
                module.GetComponent<Text>().text = "In Cooldown...";
                StartCoroutine(timer());
            }
            else
            {
                Debug.LogFormat("[Organization #{0}] The next module is now shown! '{1}'!", moduleId, order[0]);
                string temp = order[0];
                if (temp.Contains('’'))
                {
                    temp = temp.Replace("’", "\'");
                }
                else if (temp.Contains('³'))
                {
                    temp = temp.Replace('³', '3');
                }
                else if (temp.Contains('è'))
                {
                    temp = temp.Replace('è', 'e');
                }
                else if (temp.Contains('ñ'))
                {
                    temp = temp.Replace('ñ', 'n');
                }
                module.GetComponent<Text>().text = "" + temp;
            }
        }
    }

    //twitch plays
#pragma warning disable 414
    private string TwitchHelpMessage = @"No commands available";
#pragma warning restore 414
    IEnumerator ProcessTwitchCommand(string command)
    {
        if (!Settings.useSwitchVersion)
        {
            yield return null;
            yield return "sendtochaterror There are no commands for Organization.";
        }
        else
        {
            if (Regex.IsMatch(command, @"^\s*continue\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) || Regex.IsMatch(command, @"^\s*cont\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                yield return null;
                buttons[0].OnInteract();
                if (TimeModeActive == true && cooldown == true)
                {
                    yield return "sendtochat Organization is now in Cooldown!";
                }
                yield break;
            }
            if (Regex.IsMatch(command, @"^\s*toggle\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) || Regex.IsMatch(command, @"^\s*switch\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                yield return null;
                buttons[1].OnInteract();
                yield break;
            }
        }
    }

    bool TimeModeActive;
    private List<KMBombModule> TwitchAbandonModule = new List<KMBombModule>();

    IEnumerator TwitchHandleForcedSolve()
    {
        yield return null;
        module.GetComponent<Text>().text = "That's Unfortunate :(";
        moduleSolved = true;
        GetComponent<KMBombModule>().HandlePass();
    }

    class OrganizationSettings
    {
        public bool ignoreSolveBased = true;
        public bool enableMoveToBack = true;
        public bool disableTimeModeCooldown = false;
        public bool useSwitchVersion = false;
    }

    static Dictionary<string, object>[] TweaksEditorSettings = new Dictionary<string, object>[]
    {
        new Dictionary<string, object>
        {
            { "Filename", "OrganizationSettings.json" },
            { "Name", "Organization Ignore Settings" },
            { "Listing", new List<Dictionary<string, object>>{
                new Dictionary<string, object>
                {
                    { "Key", "ignoreSolveBased" },
                    { "Text", "Have Organization ignore modules that can change answers on solve" }
                },
                new Dictionary<string, object>
                {
                    { "Key", "enableMoveToBack" },
                    { "Text", "Force modules that may take a long time to solve to appear later in Organization" }
                },
                new Dictionary<string, object>
                {
                    { "Key", "disableTimeModeCooldown" },
                    { "Text", "Disables the feature of Organization performing a cooldown before its next module (any module may be solved during this time)" }
                },
                new Dictionary<string, object>
                {
                    { "Key", "useSwitchVersion" },
                    { "Text", "Reverts Organization back to its old version which has a switch to check every solve" }
                },
            } }
        }
    };
}