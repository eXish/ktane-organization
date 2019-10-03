using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;
using System;
using System.Text.RegularExpressions;
using UnityEngine.UI;

public class OrganizationScript : MonoBehaviour
{

    public KMAudio audio;
    public KMBombInfo bomb;

    public KMSelectable[] buttons;

    public static string[] fullModuleList = null;
    private string[] ignoredModules;
    private string[] backModules = new[] { "The Jewel Vault", "Turtle Robot", "Lightspeed", "Number Nimbleness", "3D Maze", "3D Tunnels", "Bomb Diffusal", "Kudosudoku", "Old Fogey", "Button Grid",
        "Reordered Keys", "Misordered Keys", "Recorded Keys", "Disordered Keys", "Simon Sings", "Vectors", "Game of Life Cruel", "Mastermind Cruel", "Factory Maze", "Simon Sends", "Quintuples",
        "The Hypercube", "The Ultracube", "Lombax Cubes", "Bamboozling Button", "Simon Stores", "The Cube", "The Sphere", "Ten-Button Color Code", "LEGOs", "Unfair Cipher", "Ultimate Cycle", "Ultimate Cipher", "Bamboozled Again" };

    private string[] ttksBefore = { "Morse Code","Wires","Two Bits","The Button","Colour Flash","Round Keypad","Password","Who's On First","Crazy Talk","Keypad","Listening","Orientation Cube" };
    private string[] ttksAfter = { "Semaphore", "Combination Lock", "Simon Says", "Astrology", "Switches", "Plumbing", "Maze", "Memory", "Complicated Wires", "Wire Sequence", "Cryptography" };
    private List<string> order = new List<string>();
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
    private bool announceMade = false;
    private bool announceMade2 = false;
    private bool otherOrgs = false;
    private int orgCount = 0;

    private OrganizationSettings Settings = new OrganizationSettings();

    void Awake()
    {
        ModConfig<OrganizationSettings> modConfig = new ModConfig<OrganizationSettings>("OrganizationSettings");
        //Read from the settings file, or create one if one doesn't exist
        Settings = modConfig.Settings;
        //Update the settings file incase there was an error during read
        modConfig.Settings = Settings;
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
        while (ignoredModules.Count() > 0)
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
    }

    void Start()
    {
        StartCoroutine(delayModStart());
    }

    void Update()
    {
        if (order.Count == 0 && detectionDone == false)
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
                    string name = getLatestSolve(bomb.GetSolvedModuleNames(), solved);
                    if (ignoredModules.Contains(name))
                    {
                        Debug.LogFormat("[Organization #{0}] Ignored module : '{1}' has been solved", moduleId, name);
                        solved.Add(name);
                        //Switch check for ignored module solve
                        getNewSwitchPos();
                    }
                    else if (cooldown == true)
                    {
                        Debug.LogFormat("[Organization #{0}] '{1}' has been solved, but due to timemode's cooldown the strike will be ignored! Removing from future possibilities...", moduleId, name);
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
                                build += order.ElementAt(i);
                            }
                            else
                            {
                                build += (order.ElementAt(i) + ", ");
                            }
                        }
                        Debug.LogFormat(build, moduleId);
                    }
                    else
                    {
                        Debug.LogFormat("[Organization #{0}] ---------------------------------------------------", moduleId);
                        if (otherOrgs == true)
                        {
                            if (readyForInput == true)
                            {
                                Debug.LogFormat("[Organization #{0}] '{1}' has been solved and this Organization needs to be manually given the next module in order to continue! Strike! Removing from future possibilities...", moduleId, name);
                                bomb.GetComponent<KMBombModule>().HandleStrike();
                                audio.GetComponent<KMAudio>().PlaySoundAtTransform("order", transform);
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
                                        build += order.ElementAt(i);
                                    }
                                    else
                                    {
                                        build += (order.ElementAt(i) + ", ");
                                    }
                                }
                                Debug.LogFormat(build, moduleId);
                                getNewSwitchPos();
                            }
                            else
                            {
                                List<string> displayed = new List<string>();
                                List<bool> ready = new List<bool>();
                                foreach (OrganizationScript mod in info.Modules)
                                {
                                    displayed.Add(mod.module.GetComponent<Text>().text);
                                    ready.Add(mod.readyForInput);
                                }
                                List<string> nrdisplayed = new List<string>();
                                for(int i = 0; i < displayed.Count; i++)
                                {
                                    if(ready.ElementAt(i) == false)
                                    {
                                        nrdisplayed.Add(displayed.ElementAt(i));
                                    }
                                }
                                if (nrdisplayed.Contains(name))
                                {
                                    if (name.Equals(order.ElementAt(0)))
                                    {
                                        solved.Add(order.ElementAt(0));
                                        order.RemoveAt(0);
                                        StartCoroutine(readyUpDelayed());
                                        arrow.GetComponent<Renderer>().enabled = true;
                                        Debug.LogFormat("[Organization #{0}] '{1}' has been solved for this Organization! Ready for next module...", moduleId, name);
                                        getNewSwitchPos();
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
                                                build += order.ElementAt(i);
                                            }
                                            else
                                            {
                                                build += (order.ElementAt(i) + ", ");
                                            }
                                        }
                                        Debug.LogFormat(build, moduleId);
                                        getNewSwitchPos();
                                    }
                                }
                                else
                                {
                                    Debug.LogFormat("[Organization #{0}] '{1}' has been solved and it was not the next module on ANY Organization! Strike! Removing from future possibilities...", moduleId, name);
                                    bomb.GetComponent<KMBombModule>().HandleStrike();
                                    audio.GetComponent<KMAudio>().PlaySoundAtTransform("order", transform);
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
                                            build += order.ElementAt(i);
                                        }
                                        else
                                        {
                                            build += (order.ElementAt(i) + ", ");
                                        }
                                    }
                                    Debug.LogFormat(build, moduleId);
                                    getNewSwitchPos();
                                }
                            }
                        }
                        else
                        {
                            if (name.Equals(order.ElementAt(0)) && (readyForInput == false))
                            {
                                solved.Add(order.ElementAt(0));
                                order.RemoveAt(0);
                                readyForInput = true;
                                Debug.LogFormat("[Organization #{0}] '{1}' has been solved! Ready for next module...", moduleId, name);
                                getNewSwitchPos();
                            }
                            else if (readyForInput == true)
                            {
                                Debug.LogFormat("[Organization #{0}] '{1}' has been solved and the module needs to be manually given the next module in order to continue! Strike! Removing from future possibilities...", moduleId, name);
                                bomb.GetComponent<KMBombModule>().HandleStrike();
                                audio.GetComponent<KMAudio>().PlaySoundAtTransform("order", transform);
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
                                        build += order.ElementAt(i);
                                    }
                                    else
                                    {
                                        build += (order.ElementAt(i) + ", ");
                                    }
                                }
                                Debug.LogFormat(build, moduleId);
                                getNewSwitchPos();
                            }
                            else
                            {
                                Debug.LogFormat("[Organization #{0}] '{1}' has been solved and it was not the next module! Strike! Removing from future possibilities...", moduleId, name);
                                bomb.GetComponent<KMBombModule>().HandleStrike();
                                audio.GetComponent<KMAudio>().PlaySoundAtTransform("order", transform);
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
                                        build += order.ElementAt(i);
                                    }
                                    else
                                    {
                                        build += (order.ElementAt(i) + ", ");
                                    }
                                }
                                Debug.LogFormat(build, moduleId);
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
        if (moduleSolved != true && cooldown != true)
        {
            audio.GetComponent<KMAudio>().PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
            pressed.AddInteractionPunch(0.25f);
            if (buttons[0] == pressed)
            {
                if (readyForInput == true)
                {
                    if (currentSwitch.Equals(nextSwitch))
                    {
                        readyForInput = false;
                        if(otherOrgs == true)
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
                            if(TimeModeActive == true)
                            {
                                audio.GetComponent<KMAudio>().PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);
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
                                audio.GetComponent<KMAudio>().PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);
                                Debug.LogFormat("[Organization #{0}] The next module is now shown! '{1}'!", moduleId, order.ElementAt(0));
                                if (order.ElementAt(0).Contains('’'))
                                {
                                    order.ElementAt(0).Replace('’', '\'');
                                }
                                else if (order.ElementAt(0).Contains('³'))
                                {
                                    order.ElementAt(0).Replace('³', '3');
                                }
                                else if (order.ElementAt(0).Contains('è'))
                                {
                                    order.ElementAt(0).Replace('è', 'e');
                                }
                                module.GetComponent<Text>().text = "" + order.ElementAt(0);
                            }
                        }
                    }
                    else
                    {
                        Debug.LogFormat("[Organization #{0}] The switch is not in the correct position! Strike!", moduleId);
                        bomb.GetComponent<KMBombModule>().HandleStrike();
                        int rand = UnityEngine.Random.Range(0, 3);
                        if (rand == 0)
                        {
                            audio.GetComponent<KMAudio>().PlaySoundAtTransform("wrong1", transform);
                        }
                        else if (rand == 1)
                        {
                            audio.GetComponent<KMAudio>().PlaySoundAtTransform("wrong2", transform);
                        }
                        else
                        {
                            audio.GetComponent<KMAudio>().PlaySoundAtTransform("wrong3", transform);
                        }
                    }
                }
                else
                {
                    Debug.LogFormat("[Organization #{0}] The current module has not been solved yet! Strike!", moduleId);
                    bomb.GetComponent<KMBombModule>().HandleStrike();
                    int rand = UnityEngine.Random.Range(0, 3);
                    if(rand == 0)
                    {
                        audio.GetComponent<KMAudio>().PlaySoundAtTransform("wrong1", transform);
                    }else if (rand == 1)
                    {
                        audio.GetComponent<KMAudio>().PlaySoundAtTransform("wrong2", transform);
                    }else
                    {
                        audio.GetComponent<KMAudio>().PlaySoundAtTransform("wrong3", transform);
                    }
                }
            }else if (buttons[1] == pressed)
            {
                if (currentSwitch.Equals("Up"))
                {
                    currentSwitch = "Down";
                    StartCoroutine(downSwitch());
                }else if (currentSwitch.Equals("Down"))
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
        order = bomb.GetSolvableModuleNames();
        List<string> remove = new List<string>();
        for (int i = 0; i < order.Count; i++)
        {
            if (ignoredModules.Contains(order.ElementAt(i)))
            {
                Debug.LogFormat("[Organization #{0}] Ignored module: '{1}' detected! Removing from possibilities...", moduleId, order.ElementAt(i));
                if(order.ElementAt(i).Equals("Turn The Keys"))
                {
                    if(announceMade2 == false)
                    {
                        ttks = true;
                        Debug.LogFormat("[Organization #{0}] TTKS detected! Keeping this in mind for Organization process!", moduleId);
                        announceMade2 = true;
                    }
                }
                if (order.ElementAt(i).Equals("Organization"))
                {
                    orgCount++;
                    if(orgCount > 1 && announceMade == false)
                    {
                        otherOrgs = true;
                        Debug.LogFormat("[Organization #{0}] Other Organizations detected! Making sure to only strike for what is not on display on ANY of the Organizations!", moduleId);
                        /**Debug.LogFormat("[Organization #{0}] Other Organizations detected! Autosolving!", moduleId);
                        module.GetComponent<Text>().text = "That's Unfortunate :(";
                        bomb.GetComponent<KMBombModule>().HandlePass();
                        moduleSolved = true;*/
                        announceMade = true;
                    }
                }
                remove.Add(order.ElementAt(i));
            }
        }
        for(int j = 0; j < remove.Count; j++)
        {
            order.Remove(remove.ElementAt(j));
        }
        order = order.Shuffle();
        if(ttks == true)
        {
            foreach (string str in order.ToList())
            {
                if (ttksBefore.Contains(str))
                {
                    order.Remove(str);
                    order.Insert(0, str);
                }else if (ttksAfter.Contains(str))
                {
                    order.Remove(str);
                    order.Add(str);
                }
            }
        }

        //Moves backModules to the end of order list
        if (Settings.enableMoveToBack)
            for (int i = 0; i < backModules.Count(); i++)
            {
                int backCount = 0;
                for (int j = 0; j < order.Count; j++)
                    if (order.ElementAt(j).Equals(backModules[i]))
                        backCount++;
                for (int j = 0; j < backCount; j++)
                    order.Remove(backModules[i]);
                for (int j = 0; j < backCount; j++)
                    order.Add(backModules[i]);
            }
        
        string build;
        if(order.Count != 0)
        {
            if (order.ElementAt(0).Contains('’'))
            {
                order.ElementAt(0).Replace('’', '\'');
            }
            else if (order.ElementAt(0).Contains('³'))
            {
                order.ElementAt(0).Replace('³', '3');
            }
            else if (order.ElementAt(0).Contains('è'))
            {
                order.ElementAt(0).Replace('è', 'e');
            }
            module.GetComponent<Text>().text = "" + order.ElementAt(0);
            build = "[Organization #{0}] The order of the non-ignored modules has been determined as: ";
        }
        else
        {
            build = "[Organization #{0}] The order of the non-ignored modules has been determined as: none";
            module.GetComponent<Text>().text = "No Modules :)";
        }
        for (int i = 0; i < order.Count; i++)
        {
            if(i == (order.Count-1))
            {
                build += order.ElementAt(i);
            }
            else
            {
                build += (order.ElementAt(i) + ", ");
            }
        }
        Debug.LogFormat(build, moduleId);
    }

    private IEnumerator readyUpDelayed()
    {
        yield return new WaitForSeconds(1.0f);
        readyForInput = true;
        StopCoroutine("readyUpDelayed");
    }

    private string getLatestSolve(List<string> s, List<string> s2)
    {
        string name = "";
        for(int i = 0; i < s2.Count; i++)
        {
            s.Remove(s2.ElementAt(i));
        }
        name = s.ElementAt(0);
        return name;
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
            if (vowelCount(module.GetComponent<Text>().text) % 2 == 0)
            {
                nextSwitch = "Down";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 2 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (solved.ElementAt(solved.Count - 1).Equals("The Digit") || solved.ElementAt(solved.Count - 1).Equals("Mega Man 2") || solved.ElementAt(solved.Count - 1).Equals("Unfair Cipher"))
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
            else if (solved.ElementAt(solved.Count - 1).Equals("The Digit") || solved.ElementAt(solved.Count - 1).Equals("Mega Man 2") || solved.ElementAt(solved.Count - 1).Equals("Unfair Cipher"))
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
            else if (solved.ElementAt(solved.Count - 1).Equals("The Digit") || solved.ElementAt(solved.Count - 1).Equals("Mega Man 2") || solved.ElementAt(solved.Count - 1).Equals("Unfair Cipher"))
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
            else if (solved.ElementAt(solved.Count - 1).Equals("The Digit") || solved.ElementAt(solved.Count - 1).Equals("Mega Man 2") || solved.ElementAt(solved.Count - 1).Equals("Unfair Cipher"))
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
        char[] vowels = { 'A','E','O','I','U' };
        s = s.ToUpper();
        for(int i = 0; i < s.Length; i++)
        {
            if (vowels.Contains(s.ElementAt(i)))
            {
                vowcount++;
            }
        }
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

    private IEnumerator delayModStart()
    {
        yield return new WaitForSeconds(1f);
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
        Debug.LogFormat("[Organization #{0}] TimeMode Cooldown Active: '{1}'", moduleId, TimeModeActive);
        generateOrder();
        if (bomb.GetSolvableModuleNames().Where(x => !ignoredModules.Contains(x)).Count() == 0)
        {
            getNewSwitchPos();
        }
    }

    private IEnumerator timer()
    {
        cooldown = true;
        double counter = UnityEngine.Random.Range(30, 46);
        if(otherOrgs == true)
        {
            Debug.LogFormat("[Organization #{0}] Cooldown activated! There is {1} seconds of free solving until the next module is shown!", moduleId, (int)counter);
        }
        else
        {
            Debug.LogFormat("[Organization #{0}] Cooldown activated! There is {1} seconds until the next module is shown! (No guaranteed free solves due to other orgs)", moduleId, (int)counter);
        }
        while(counter > 0)
        {
            yield return new WaitForSeconds(0.1f);
            if(counter < 11)
            {
                module.GetComponent<Text>().text = ""+(int)counter;
            }
            counter -= 0.1;
        }
        cooldown = false;
        audio.GetComponent<KMAudio>().PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.NeedyActivated, transform);
        Debug.LogFormat("[Organization #{0}] Cooldown over! The next module is now shown! '{1}'!", moduleId, order.ElementAt(0));
        if (order.ElementAt(0).Contains('’'))
        {
            order.ElementAt(0).Replace('’', '\'');
        }
        else if (order.ElementAt(0).Contains('³'))
        {
            order.ElementAt(0).Replace('³', '3');
        }
        else if (order.ElementAt(0).Contains('è'))
        {
            order.ElementAt(0).Replace('è', 'e');
        }
        module.GetComponent<Text>().text = "" + order.ElementAt(0);
        StopCoroutine("timer");
    }

    //twitch plays
    #pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} continue/cont [Presses the continue button] | !{0} toggle/switch [Toggles the switch to move to the other positon (positions are either up or down)]";
    #pragma warning restore 414
    IEnumerator ProcessTwitchCommand(string command)
    {
        if (Regex.IsMatch(command, @"^\s*continue\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) || Regex.IsMatch(command, @"^\s*cont\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            buttons[0].OnInteract();
            yield return new WaitForSeconds(0.2f);
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

    bool TimeModeActive;

    IEnumerator TwitchHandleForcedSolve()
    {
        yield return null;
        module.GetComponent<Text>().text = "That's Unfortunate :(";
        moduleSolved = true;
    }

    class OrganizationSettings
    {
        public bool ignoreSolveBased = true;
        public bool enableMoveToBack = true;
        public bool disableTimeModeCooldown = false;
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
            } }
        }
    };
}