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

    public static string[] ignoredModules = null;

    private string[] ttksBefore = { "Morse Code","Wires","Two Bits","The Button","Colour Flash","Round Keypad","Password","Who's On First","Crazy Talk","Keypad","Listening","Orientation Cube" };
    private string[] ttksAfter = { "Semaphore", "Combination Lock", "Simon Says", "Astrology", "Switches", "Plumbing", "Maze", "Memory", "Complicated Wires", "Wire Sequence", "Cryptography" };
    private List<string> order = new List<string>();
    private List<string> solved = new List<string>();

    private string nextSwitch;
    private string currentSwitch;

    public GameObject module;

    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;

    private bool readyForInput = false;
    private bool detectionDone = false;
    private bool announceMade = false;
    private bool announceMade2 = false;
    //these will be used in the future when multiple organization support is made
    private bool otherOrgs = false;
    private int orgCount = 0;

    void Awake()
    {
        nextSwitch = "";
        currentSwitch = "Up";
        moduleId = moduleIdCounter++;
        moduleSolved = false;
        foreach (KMSelectable obj in buttons)
        {
            KMSelectable pressed = obj;
            pressed.OnInteract += delegate () { PressButton(pressed); return false; };
        }
        if (ignoredModules == null)
            ignoredModules = GetComponent<KMBossModule>().GetIgnoredModules("Organization", new string[]{
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

    void Start()
    {
        generateOrder();
        if(bomb.GetSolvableModuleNames().Where(x => !ignoredModules.Contains(x)).Count() == 0)
        {
            getNewSwitchPos();
        }
    }

    void Update()
    {
        if (order.Count == 0 && detectionDone == false)
        {
            module.GetComponent<Text>().text = "No Modules :)";
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
                        solved.Add(name);
                    }
                    else
                    {
                        Debug.LogFormat("[Organization #{0}] ---------------------------------------------------", moduleId);
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

    void PressButton(KMSelectable pressed)
    {
        if(moduleSolved != true)
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
                        if (order.Count == 0)
                        {
                            Debug.LogFormat("[Organization #{0}] All non-ignored modules solved! GG!", moduleId);
                            moduleSolved = true;
                            bomb.GetComponent<KMBombModule>().HandlePass();
                        }
                        else
                        {
                            audio.GetComponent<KMAudio>().PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);
                            Debug.LogFormat("[Organization #{0}] The next module is now shown! '{1}'!", moduleId, order.ElementAt(0));
                            module.GetComponent<Text>().text = "" + order.ElementAt(0);
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
                        //Debug.LogFormat("[Organization #{0}] Other Organizations detected! Making sure to only strike for what is not on display on ANY of the Organizations!", moduleId);
                        Debug.LogFormat("[Organization #{0}] Other Organizations detected! Autosolving!", moduleId);
                        module.GetComponent<Text>().text = "That's Unfortunate :(";
                        bomb.GetComponent<KMBombModule>().HandlePass();
                        moduleSolved = true;
                        announceMade = true;
                        return;
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
        //Moves The Jewel Vault to end of order list
        int jewelcount = 0;
        for (int i = 0; i < order.Count; i++)
        {
            if (order.ElementAt(i).Equals("The Jewel Vault"))
            {
                jewelcount++;
            }
        }
        for (int i = 0; i < jewelcount; i++)
        {
            order.Remove("The Jewel Vault");
        }
        for (int i = 0; i < jewelcount; i++)
        {
            order.Add("The Jewel Vault");
        }
        //Moves Turtle Robot to end of order list
        int robotcount = 0;
        for (int i = 0; i < order.Count; i++)
        {
            if (order.ElementAt(i).Equals("Turtle Robot"))
            {
                robotcount++;
            }
        }
        for (int i = 0; i < robotcount; i++)
        {
            order.Remove("Turtle Robot");
        }
        for (int i = 0; i < robotcount; i++)
        {
            order.Add("Turtle Robot");
        }
        //Moves Lightspeed to end of order list
        int lightcount = 0;
        for (int i = 0; i < order.Count; i++)
        {
            if (order.ElementAt(i).Equals("Lightspeed"))
            {
                lightcount++;
            }
        }
        for (int i = 0; i < lightcount; i++)
        {
            order.Remove("Lightspeed");
        }
        for (int i = 0; i < lightcount; i++)
        {
            order.Add("Lightspeed");
        }
        //Moves Number Nimbleness to end of order list
        int numbercount = 0;
        for (int i = 0; i < order.Count; i++)
        {
            if (order.ElementAt(i).Equals("Number Nimbleness"))
            {
                numbercount++;
            }
        }
        for (int i = 0; i < numbercount; i++)
        {
            order.Remove("Number Nimbleness");
        }
        for (int i = 0; i < numbercount; i++)
        {
            order.Add("Number Nimbleness");
        }
        //Moves 3D Maze to end of order list
        int tdcount = 0;
        for (int i = 0; i < order.Count; i++)
        {
            if (order.ElementAt(i).Equals("3D Maze"))
            {
                tdcount++;
            }
        }
        for (int i = 0; i < tdcount; i++)
        {
            order.Remove("3D Maze");
        }
        for (int i = 0; i < tdcount; i++)
        {
            order.Add("3D Maze");
        }
        //Moves Bomb Diffusal to end of order list
        int diffusalcount = 0;
        for (int i = 0; i < order.Count; i++)
        {
            if (order.ElementAt(i).Equals("Bomb Diffusal"))
            {
                diffusalcount++;
            }
        }
        for (int i = 0; i < diffusalcount; i++)
        {
            order.Remove("Bomb Diffusal");
        }
        for (int i = 0; i < diffusalcount; i++)
        {
            order.Add("Bomb Diffusal");
        }
        //Moves Old Fogey to end of order list
        int fogeycount = 0;
        for (int i = 0; i < order.Count; i++)
        {
            if (order.ElementAt(i).Equals("Old Fogey"))
            {
                fogeycount++;
            }
        }
        for (int i = 0; i < fogeycount; i++)
        {
            order.Remove("Old Fogey");
        }
        for (int i = 0; i < fogeycount; i++)
        {
            order.Add("Old Fogey");
        }
        //Moves Button Grid to end of order list
        int gridcount = 0;
        for (int i = 0; i < order.Count; i++)
        {
            if (order.ElementAt(i).Equals("Button Grid"))
            {
                gridcount++;
            }
        }
        for (int i = 0; i < gridcount; i++)
        {
            order.Remove("Button Grid");
        }
        for (int i = 0; i < gridcount; i++)
        {
            order.Add("Button Grid");
        }
        //Moves Simon Sings to end of order list
        int singscount = 0;
        for (int i = 0; i < order.Count; i++)
        {
            if (order.ElementAt(i).Equals("Simon Sings"))
            {
                singscount++;
            }
        }
        for (int i = 0; i < singscount; i++)
        {
            order.Remove("Simon Sings");
        }
        for (int i = 0; i < singscount; i++)
        {
            order.Add("Simon Sings");
        }
        //Moves Vectors to end of order list
        int vectorcount = 0;
        for (int i = 0; i < order.Count; i++)
        {
            if (order.ElementAt(i).Equals("Vectors"))
            {
                vectorcount++;
            }
        }
        for (int i = 0; i < vectorcount; i++)
        {
            order.Remove("Vectors");
        }
        for (int i = 0; i < vectorcount; i++)
        {
            order.Add("Vectors");
        }
        //Moves Game of Life Cruel to end of order list
        int gofcount = 0;
        for (int i = 0; i < order.Count; i++)
        {
            if (order.ElementAt(i).Equals("Game of Life Cruel"))
            {
                gofcount++;
            }
        }
        for (int i = 0; i < gofcount; i++)
        {
            order.Remove("Game of Life Cruel");
        }
        for (int i = 0; i < gofcount; i++)
        {
            order.Add("Game of Life Cruel");
        }
        //Moves Mastermind Cruel to end of order list
        int mccount = 0;
        for (int i = 0; i < order.Count; i++)
        {
            if (order.ElementAt(i).Equals("Mastermind Cruel"))
            {
                mccount++;
            }
        }
        for (int i = 0; i < mccount; i++)
        {
            order.Remove("Mastermind Cruel");
        }
        for (int i = 0; i < mccount; i++)
        {
            order.Add("Mastermind Cruel");
        }
        //Moves Simon Sends to end of order list
        int sendscount = 0;
        for (int i = 0; i < order.Count; i++)
        {
            if (order.ElementAt(i).Equals("Simon Sends"))
            {
                sendscount++;
            }
        }
        for (int i = 0; i < sendscount; i++)
        {
            order.Remove("Simon Sends");
        }
        for (int i = 0; i < sendscount; i++)
        {
            order.Add("Simon Sends");
        }
        //Moves The Hypercube to end of order list
        int hypercount = 0;
        for (int i = 0; i < order.Count; i++)
        {
            if (order.ElementAt(i).Equals("The Hypercube"))
            {
                hypercount++;
            }
        }
        for (int i = 0; i < hypercount; i++)
        {
            order.Remove("The Hypercube");
        }
        for (int i = 0; i < hypercount; i++)
        {
            order.Add("The Hypercube");
        }
        //Moves The Ultracube to end of order list
        int ultracount = 0;
        for (int i = 0; i < order.Count; i++)
        {
            if (order.ElementAt(i).Equals("The Ultracube"))
            {
                ultracount++;
            }
        }
        for (int i = 0; i < ultracount; i++)
        {
            order.Remove("The Ultracube");
        }
        for (int i = 0; i < ultracount; i++)
        {
            order.Add("The Ultracube");
        }
        //Moves Lombax Cubes to end of order list
        int lombcount = 0;
        for (int i = 0; i < order.Count; i++)
        {
            if (order.ElementAt(i).Equals("Lombax Cubes"))
            {
                lombcount++;
            }
        }
        for (int i = 0; i < lombcount; i++)
        {
            order.Remove("Lombax Cubes");
        }
        for (int i = 0; i < lombcount; i++)
        {
            order.Add("Lombax Cubes");
        }
        //Moves Bamboozling Button to end of order list
        int bamBcount = 0;
        for (int i = 0; i < order.Count; i++)
        {
            if (order.ElementAt(i).Equals("Bamboozling Button"))
            {
                bamBcount++;
            }
        }
        for (int i = 0; i < bamBcount; i++)
        {
            order.Remove("Bamboozling Button");
        }
        for (int i = 0; i < bamBcount; i++)
        {
            order.Add("Bamboozling Button");
        }
        //Moves Simon Stores to end of order list
        int storescount = 0;
        for (int i = 0; i < order.Count; i++)
        {
            if (order.ElementAt(i).Equals("Simon Stores"))
            {
                storescount++;
            }
        }
        for (int i = 0; i < storescount; i++)
        {
            order.Remove("Simon Stores");
        }
        for (int i = 0; i < storescount; i++)
        {
            order.Add("Simon Stores");
        }
        //Moves The Cube to end of order list
        int cubecount = 0;
        for (int i = 0; i < order.Count; i++)
        {
            if (order.ElementAt(i).Equals("The Cube"))
            {
                cubecount++;
            }
        }
        for (int i = 0; i < cubecount; i++)
        {
            order.Remove("The Cube");
        }
        for (int i = 0; i < cubecount; i++)
        {
            order.Add("The Cube");
        }
        //Moves The Sphere to end of order list
        int spherecount = 0;
        for (int i = 0; i < order.Count; i++)
        {
            if (order.ElementAt(i).Equals("The Sphere"))
            {
                spherecount++;
            }
        }
        for (int i = 0; i < spherecount; i++)
        {
            order.Remove("The Sphere");
        }
        for (int i = 0; i < spherecount; i++)
        {
            order.Add("The Sphere");
        }
        //Moves LEGOs to end of order list
        int legocount = 0;
        for (int i = 0; i < order.Count; i++)
        {
            if (order.ElementAt(i).Equals("LEGOs"))
            {
                legocount++;
            }
        }
        for (int i = 0; i < legocount; i++)
        {
            order.Remove("LEGOs");
        }
        for (int i = 0; i < legocount; i++)
        {
            order.Add("LEGOs");
        }
        //Moves Unfair Cipher to end of order list
        int unfaircount = 0;
        for (int i = 0; i < order.Count; i++)
        {
            if (order.ElementAt(i).Equals("Unfair Cipher"))
            {
                unfaircount++;
            }
        }
        for (int i = 0; i < unfaircount; i++)
        {
            order.Remove("Unfair Cipher");
        }
        for (int i = 0; i < unfaircount; i++)
        {
            order.Add("Unfair Cipher");
        }
        //Moves Ultimate Cipher to end of order list
        int ciphercount = 0;
        for (int i = 0; i < order.Count; i++)
        {
            if (order.ElementAt(i).Equals("Ultimate Cipher"))
            {
                ciphercount++;
            }
        }
        for (int i = 0; i < ciphercount; i++)
        {
            order.Remove("Ultimate Cipher");
        }
        for (int i = 0; i < ciphercount; i++)
        {
            order.Add("Ultimate Cipher");
        }
        //Moves Bamboozled Again to end of order list
        int bamcount = 0;
        for(int i = 0; i < order.Count; i++)
        {
            if(order.ElementAt(i).Equals("Bamboozled Again"))
            {
                bamcount++;
            }
        }
        for (int i = 0; i < bamcount; i++)
        {
            order.Remove("Bamboozled Again");
        }
        for (int i = 0; i < bamcount; i++)
        {
            order.Add("Bamboozled Again");
        }
        string build;
        if(order.Count != 0)
        {
            module.GetComponent<Text>().text = "" + order.ElementAt(0);
            build = "[Organization #{0}] The order of the non-ignored modules has been determined as: ";
        }
        else
        {
            build = "[Organization #{0}] The order of the non-ignored modules has been determined as: none";
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
            if (solved.ElementAt(solved.Count - 1).Equals("DetoNATO"))
            {
                nextSwitch = "Down";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 2 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (solved.ElementAt(solved.Count - 1).Equals("The London Underground") || solved.ElementAt(solved.Count - 1).Equals("Unfair Cipher"))
            {
                nextSwitch = "Up";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 3 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (solved.ElementAt(0).Equals("3D Tunnels"))
            {
                nextSwitch = "Down";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 5 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (bomb.IsIndicatorPresent("SND"))
            {
                nextSwitch = "Up";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 6 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (bomb.GetModuleNames().Contains("Forget This"))
            {
                nextSwitch = "Down";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 7 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (bomb.GetBatteryCount() <= 2)
            {
                nextSwitch = "Up";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 8 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else
            {
                nextSwitch = "Down";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 9 (no others true rule) is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
        }
        else if (solved.Count == 2)
        {
            if (solved.ElementAt(solved.Count - 1).Equals("DetoNATO"))
            {
                nextSwitch = "Down";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 2 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (solved.ElementAt(solved.Count - 1).Equals("The London Underground") || solved.ElementAt(solved.Count - 1).Equals("Unfair Cipher"))
            {
                nextSwitch = "Up";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 3 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (solved.ElementAt(0).Equals("3D Tunnels") || solved.ElementAt(1).Equals("3D Tunnels"))
            {
                nextSwitch = "Down";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 5 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (bomb.IsIndicatorPresent("SND"))
            {
                nextSwitch = "Up";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 6 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (bomb.GetModuleNames().Contains("Forget This"))
            {
                nextSwitch = "Down";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 7 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (bomb.GetBatteryCount() <= 2)
            {
                nextSwitch = "Up";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 8 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else
            {
                nextSwitch = "Down";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 9 (no others true rule) is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
        }
        else if (solved.Count == 3)
        {
            nextSwitch = "Up";
            Debug.LogFormat("[Organization #{0}] Switch Table Rule 1 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
        }
        else if (solved.Count == 4)
        {
            if (solved.ElementAt(solved.Count - 1).Equals("DetoNATO"))
            {
                nextSwitch = "Down";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 2 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (solved.ElementAt(solved.Count - 1).Equals("The London Underground") || solved.ElementAt(solved.Count - 1).Equals("Unfair Cipher"))
            {
                nextSwitch = "Up";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 3 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (solved.ElementAt(2).Equals("Alphabet"))
            {
                nextSwitch = "Down";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 4 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (solved.ElementAt(0).Equals("3D Tunnels") || solved.ElementAt(1).Equals("3D Tunnels"))
            {
                nextSwitch = "Down";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 5 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (bomb.IsIndicatorPresent("SND"))
            {
                nextSwitch = "Up";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 6 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (bomb.GetModuleNames().Contains("Forget This"))
            {
                nextSwitch = "Down";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 7 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (bomb.GetBatteryCount() <= 2)
            {
                nextSwitch = "Up";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 8 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else
            {
                nextSwitch = "Down";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 9 (no others true rule) is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
        }
        else
        {
            if (((bomb.GetSolvedModuleNames().Count % 3) == 0))
            {
                nextSwitch = "Up";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 1 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (solved.ElementAt(solved.Count - 1).Equals("DetoNATO"))
            {
                nextSwitch = "Down";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 2 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (solved.ElementAt(solved.Count - 1).Equals("The London Underground") || solved.ElementAt(solved.Count - 1).Equals("Unfair Cipher"))
            {
                nextSwitch = "Up";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 3 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (solved.ElementAt(2).Equals("Alphabet") || solved.ElementAt(4).Equals("Alphabet"))
            {
                nextSwitch = "Down";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 4 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (solved.ElementAt(0).Equals("3D Tunnels") || solved.ElementAt(1).Equals("3D Tunnels"))
            {
                nextSwitch = "Down";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 5 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (bomb.IsIndicatorPresent("SND"))
            {
                nextSwitch = "Up";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 6 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (bomb.GetModuleNames().Contains("Forget This"))
            {
                nextSwitch = "Down";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 7 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else if (bomb.GetBatteryCount() <= 2)
            {
                nextSwitch = "Up";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 8 is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
            else
            {
                nextSwitch = "Down";
                Debug.LogFormat("[Organization #{0}] Switch Table Rule 9 (no others true rule) is true! This makes the new required switch position '{1}'!", moduleId, nextSwitch);
            }
        }
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
            yield break;
        }
        if (Regex.IsMatch(command, @"^\s*toggle\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) || Regex.IsMatch(command, @"^\s*switch\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            buttons[1].OnInteract();
            yield break;
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        yield return null;
        module.GetComponent<Text>().text = "That's Unfortunate :(";
        moduleSolved = true;
    }
}