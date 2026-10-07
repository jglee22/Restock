using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using TMPro;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using UnityEngine.Video;

// 포트폴리오 원본 촬영 전용. 플레이 빌드에 포함되지 않는다.
[InitializeOnLoad]
public static class RestockPortfolioCapture
{
    const string KeyRun = "RestockCapture.Run";
    const string KeyCut = "RestockCapture.Cut";
    const string KeyMax = "RestockCapture.Max";
    const string ExpectedSaveHash = "213CD36384D049982657B437AC7FEE1607E873EF6EB1607576D4CE4BA4B5AABD";
    const int Width = 1920;
    const int Height = 1080;
    const float Rate = 60f;

    static int step;
    static int clickPhase;
    static int keyPhase;
    static int markerFrame;
    static int saleFrame = -1;
    static int revenueAtStart;
    static double markerTime;
    static double cutStart;
    static double nextStatus;
    static Vector2 clickPos;
    static int moneyMark;
    static int shelfTry;
    static bool skippedAhead;
    static bool acted;
    static int stockBefore = -1;
    static Shelf targetShelf;
    static Vector2 targetScreen;
    static List<Vector3> placeCells;
    static int placeIndex;
    static Vector3 placePoint;
    static int shelvesBefore;
    static float bestPlaceScore;
    static bool foundPlace;
    static RecorderController controller;
    static VideoPlayer probe;
    static string probePath;
    static string activeName;

    static RestockPortfolioCapture()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    [MenuItem("Restock/Capture/Record 01-02")]
    public static void Begin()
    {
        if (SessionState.GetInt(KeyRun, 0) == 1)
        {
            Log("already running");
            return;
        }

        if (!PrepareSaveBackup())
        {
            return;
        }

        if (!PrepareScene())
        {
            return;
        }

        PrepareView();
        Directory.CreateDirectory(OutputFolder());
        SessionState.SetInt(KeyCut, 0);
        SessionState.SetInt(KeyMax, 2);
        SessionState.SetInt(KeyRun, 1);
        Log("begin cuts 01-02");
        EnterPlay();
    }

    [MenuItem("Restock/Capture/Record 03-10")]
    public static void Continue()
    {
        RecordCuts(SessionState.GetInt(KeyCut, 0), 9);
    }

    public static void RecordCuts(int startInclusive, int endExclusive)
    {
        if (!File.Exists(BackupPath()) && !PrepareSaveBackup())
        {
            return;
        }

        PrepareView();
        SessionState.SetInt(KeyCut, startInclusive);
        SessionState.SetInt(KeyMax, endExclusive);
        SessionState.SetInt(KeyRun, 1);
        Log("record range " + startInclusive + "-" + endExclusive);
        EnterPlay();
    }

    [MenuItem("Restock/Capture/Abort")]
    public static void Abort()
    {
        StopMovie();
        DestroyProbe();
        SessionState.SetInt(KeyRun, 0);
        RestoreSave();
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
        }

        Log("aborted");
    }

    static void Tick()
    {
        if (SessionState.GetInt(KeyRun, 0) != 1)
        {
            return;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            return;
        }

        if (!EditorApplication.isPlaying)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (SessionState.GetInt(KeyCut, 0) >= SessionState.GetInt(KeyMax, 0))
            {
                Finish();
                return;
            }

            EnterPlay();
            return;
        }

        if (!Application.isPlaying || Time.frameCount < 15)
        {
            return;
        }

        if (EditorApplication.timeSinceStartup >= nextStatus)
        {
            nextStatus = EditorApplication.timeSinceStartup + 5d;
            WriteStatus();
        }

        if (cutStart > 0d && EditorApplication.timeSinceStartup - cutStart > 450d)
        {
            Log("timeout");
            Fail();
            return;
        }

        try
        {
            int cut = SessionState.GetInt(KeyCut, 0);
            switch (cut)
            {
                case 0:
                    CutHero();
                    break;
                case 1:
                    CutStarter();
                    break;
                case 2:
                    CutOrder();
                    break;
                case 3:
                    CutCustomer();
                    break;
                case 4:
                    CutGoalAndExpansion();
                    break;
                case 5:
                    CutAssign();
                    break;
                case 6:
                    CutExpanded();
                    break;
                case 7:
                    CutBuild();
                    break;
                case 8:
                    CutCampaign();
                    break;
                default:
                    SessionState.SetInt(KeyCut, SessionState.GetInt(KeyMax, 0));
                    EditorApplication.isPlaying = false;
                    break;
            }
        }
        catch (Exception exception)
        {
            Log("exception " + exception);
            Fail();
        }
    }

    static void CutHero()
    {
        if (step == 0)
        {
            BeginCut();
            PrepareView();
            EnsureDevices();
            StockStore(4, 500000, true);
            var session = UnityEngine.Object.FindAnyObjectByType<StoreSession>();
            session.StartBusiness();
            session.SetSpeed(StoreSession.DoubleTimeScale);
            LogHud("hero");
            Log("hero open customers " + CustomerCount());
            EnterStep(1);
            return;
        }

        if (step == 1)
        {
            if (CustomerCount() >= 3 || ElapsedRealtime() > 25d)
            {
                Log("hero customers " + CustomerCount() + " revenue " + Revenue());
                EnterStep(2);
            }

            return;
        }

        if (step == 2)
        {
            if (!StartMovie("01_HeroStore_R2"))
            {
                Fail();
                return;
            }

            EnterStep(3);
            return;
        }

        if (step == 3)
        {
            RepaintGame();
            if (ElapsedFrames() >= 9 * 60)
            {
                StopMovie();
                BeginProbe("01_HeroStore_R2");
                EnterStep(4);
            }

            return;
        }

        if (PollProbe(true))
        {
            NextCut();
        }
    }

    static void CutStarter()
    {
        if (step == 0)
        {
            BeginCut();
            PrepareView();
            EnsureDevices();
            Log("starter phase " + Phase());
            EnterStep(1);
            return;
        }

        if (step == 1)
        {
            if (ElapsedRealtime() < 1d)
            {
                return;
            }

            if (!StartMovie("02_StarterStore"))
            {
                Fail();
                return;
            }

            EnterStep(2);
            return;
        }

        if (step == 2)
        {
            RepaintGame();
            if (ElapsedFrames() >= 7 * 60)
            {
                StopMovie();
                BeginProbe("02_StarterStore");
                EnterStep(3);
            }

            return;
        }

        if (PollProbe(true))
        {
            NextCut();
        }
    }

    static void CutOrder()
    {
        if (step == 0)
        {
            BeginCut();
            PrepareView();
            EnsureDevices();
            var economy = UnityEngine.Object.FindAnyObjectByType<StoreEconomy>();
            economy.TryRestoreState(30000, 0, 0);
            Log("order money " + economy.CurrentMoney);
            EnterStep(1);
            return;
        }

        if (step == 1)
        {
            if (ElapsedRealtime() < 0.5d)
            {
                return;
            }

            if (!StartMovie("03_OrderInventory"))
            {
                Fail();
                return;
            }

            EnterStep(2);
            return;
        }

        if (step == 2)
        {
            RepaintGame();
            if (ElapsedFrames() < 70)
            {
                return;
            }

            if (ClickButton(Hud(), "orderToggleButton"))
            {
                EnterStep(3);
            }

            return;
        }

        if (step == 3)
        {
            RepaintGame();
            if (ElapsedFrames() < 40)
            {
                return;
            }

            Button order = FirstOrderButton();
            if (order == null)
            {
                Log("order row missing");
                EnterStep(4);
                return;
            }

            if (clickPhase == 0 && !Reveal(order.GetComponent<RectTransform>(), out clickPos) && ElapsedFrames() < 120)
            {
                return;
            }

            if (ClickAt(clickPos))
            {
                Log("order clicked money " + Money());
                EnterStep(4);
            }

            return;
        }

        if (step == 4)
        {
            RepaintGame();
            if (ElapsedFrames() >= 420)
            {
                Log("order end money " + Money());
                StopMovie();
                BeginProbe("03_OrderInventory");
                EnterStep(5);
            }

            return;
        }

        if (PollProbe(false))
        {
            NextCut();
        }
    }

    static void CutCustomer()
    {
        if (step == 0)
        {
            BeginCut();
            PrepareView();
            EnsureDevices();
            FrameStarterStore();
            LogHud("customer");
            EnterStep(1);
            return;
        }

        if (step == 1)
        {
            var session = UnityEngine.Object.FindAnyObjectByType<StoreSession>();
            session.StartBusiness();
            session.SetSpeed(StoreSession.NormalTimeScale);
            EnterStep(2);
            return;
        }

        if (step == 2)
        {
            if (CustomerCount() >= 3 || ElapsedRealtime() > 22d)
            {
                revenueAtStart = Revenue();
                saleFrame = -1;
                if (!StartMovie("04_CustomerLoop_R2"))
                {
                    Fail();
                    return;
                }

                EnterStep(3);
            }

            return;
        }

        if (step == 3)
        {
            RepaintGame();
            int revenue = Revenue();
            if (saleFrame < 0 && revenue > revenueAtStart)
            {
                saleFrame = Time.frameCount;
                Log("sale revenue " + revenue);
            }

            bool sold = saleFrame >= 0 && Time.frameCount - saleFrame >= 150;
            bool longEnough = ElapsedFrames() >= 10 * 60;
            bool tooLong = ElapsedFrames() >= 12 * 60;
            if ((sold && longEnough) || tooLong)
            {
                Log("customer end revenue " + revenue + " customers " + CustomerCount());
                StopMovie();
                BeginProbe("04_CustomerLoop_R2");
                EnterStep(4);
            }

            return;
        }

        if (PollProbe(false))
        {
            NextCut();
        }
    }

    static void CutGoalAndExpansion()
    {
        if (step == 0)
        {
            BeginCut();
            PrepareView();
            EnsureDevices();
            skippedAhead = false;
            var session = UnityEngine.Object.FindAnyObjectByType<StoreSession>();
            session.StartBusiness();
            session.SetSpeed(StoreSession.TripleTimeScale);
            Log("goal day start");
            EnterStep(1);
            return;
        }

        if (step == 1)
        {
            RunOpenDay();
            var session = UnityEngine.Object.FindAnyObjectByType<StoreSession>();
            bool ready = session.Phase == StorePhase.Closing && CustomerCount() == 0;
            bool gaveUp = ElapsedRealtime() > 340d;
            if (ready || gaveUp)
            {
                var progression = UnityEngine.Object.FindAnyObjectByType<StoreProgression>();
                Log("goal ready phase " + session.Phase + " revenue " + Revenue() + " succeeded-before-result " + progression.LastSucceeded);
                if (!StartMovie("05_GoalResult"))
                {
                    Fail();
                    return;
                }

                EnterStep(2);
            }

            return;
        }

        if (step == 2)
        {
            RepaintGame();
            if (ElapsedFrames() < 70)
            {
                return;
            }

            if (ClickButton(Hud(), "showResultButton"))
            {
                var progression = UnityEngine.Object.FindAnyObjectByType<StoreProgression>();
                Log("result succeeded " + progression.LastSucceeded + " revenue " + progression.LastRevenue + " goal " + progression.LastGoal);
                EnterStep(3);
            }

            return;
        }

        if (step == 3)
        {
            RepaintGame();
            if (ElapsedFrames() >= 300)
            {
                StopMovie();
                BeginProbe("05_GoalResult");
                EnterStep(4);
            }

            return;
        }

        if (step == 4)
        {
            if (!PollProbe(false))
            {
                return;
            }

            if (!StartMovie("06_StoreExpansion"))
            {
                Fail();
                return;
            }

            Log("shelves before next day " + ActiveShelfCount());
            EnterStep(5);
            return;
        }

        if (step == 5)
        {
            RepaintGame();
            if (ElapsedFrames() < 60)
            {
                return;
            }

            if (ClickButton(Hud(), "nextDayButton"))
            {
                Log("shelves after next day " + ActiveShelfCount() + " stage " + UnityEngine.Object.FindAnyObjectByType<StoreProgression>().CompletedStage);
                EnterStep(6);
            }

            return;
        }

        if (step == 6)
        {
            RepaintGame();
            if (ElapsedFrames() >= 360)
            {
                StopMovie();
                BeginProbe("06_StoreExpansion");
                EnterStep(7);
            }

            return;
        }

        if (PollProbe(false))
        {
            NextCut();
        }
    }

    static void CutAssign()
    {
        if (step == 0)
        {
            BeginCut();
            PrepareView();
            EnsureDevices();
            StockStore(2, 400000, false);
            LogHud("assign");
            var chips = FindProduct("chips_original");
            var ordering = FindInactive<StoreOrdering>();
            bool ordered = chips != null && ordering != null && ordering.TryOrder(chips, 16);
            Log("assign prep chips " + ordered + " empty " + (FindEmptyWall() != null));
            EnterStep(1);
            return;
        }

        if (step == 1)
        {
            if (ElapsedRealtime() < 0.5d)
            {
                return;
            }

            if (!StartMovie("07_AssignRestock_R2"))
            {
                Fail();
                return;
            }

            EnterStep(2);
            return;
        }

        if (step == 2)
        {
            RepaintGame();
            if (ElapsedFrames() < 45)
            {
                return;
            }

            if (PanelActive(Hud(), "orderPanel"))
            {
                moneyMark = Money();
                EnterStep(3);
                return;
            }

            ClickButton(Hud(), "orderToggleButton");
            return;
        }

        if (step == 3)
        {
            RepaintGame();
            Button order = ChipsOrderButton();
            if (order == null || !order.gameObject.activeInHierarchy)
            {
                if (ElapsedFrames() > 150)
                {
                    Log("chips row missing");
                    EnterStep(4);
                }

                return;
            }

            var scroll = order.GetComponentInParent<ScrollRect>();
            if (ElapsedFrames() < 12)
            {
                if (scroll != null)
                {
                    scroll.verticalNormalizedPosition = 0f;
                }

                return;
            }

            ProductDefinition chips = FindProduct("chips_original");
            var inventory = UnityEngine.Object.FindAnyObjectByType<StoreInventory>();
            int quantity = chips != null && inventory != null ? inventory.GetQuantity(chips) : -1;
            if (stockBefore < 0)
            {
                stockBefore = quantity;
            }

            if (quantity > stockBefore)
            {
                Log("chips stock " + stockBefore + " -> " + quantity);
                EnterStep(4);
                return;
            }

            Vector2 screen = ScreenOf(order.GetComponent<RectTransform>());
            if (!PointerHits(screen, order))
            {
                if (scroll != null && ElapsedFrames() % 8 == 0)
                {
                    scroll.verticalNormalizedPosition = Mathf.Repeat(scroll.verticalNormalizedPosition + 0.12f, 1.01f);
                }

                if (ElapsedFrames() > 140)
                {
                    Log("chips not clickable at " + screen);
                    EnterStep(4);
                }

                return;
            }

            if (!acted)
            {
                if (ClickAt(screen))
                {
                    acted = true;
                    Log("chips click " + screen);
                }

                return;
            }

            if (ElapsedFrames() > 180)
            {
                Log("chips order unchanged " + quantity);
                EnterStep(4);
            }

            return;
        }

        if (step == 4)
        {
            RepaintGame();
            if (ElapsedFrames() < 100)
            {
                return;
            }

            if (PanelActive(Hud(), "orderPanel") && ElapsedFrames() < 180)
            {
                ClickButton(Hud(), "orderToggleButton");
                return;
            }

            Log("order closed money " + Money() + " was " + moneyMark);
            EnterStep(5);
            return;
        }

        if (step == 5)
        {
            RepaintGame();
            if (ElapsedFrames() < 25)
            {
                return;
            }

            var build = UnityEngine.Object.FindAnyObjectByType<BuildModeController>();
            if (!PanelActive(build, "buildPanel"))
            {
                ClickButton(build, "buildButton");
                return;
            }

            EnterStep(6);
            return;
        }

        if (step == 6)
        {
            RepaintGame();
            if (ElapsedFrames() < 20)
            {
                return;
            }

            var build = UnityEngine.Object.FindAnyObjectByType<BuildModeController>();
            if (ClickButton(build, "productButton"))
            {
                shelfTry = 0;
                EnterStep(7);
            }

            return;
        }

        if (step == 7)
        {
            RepaintGame();
            if (ElapsedFrames() < 15)
            {
                return;
            }

            if (!TryPickEmptyWall(shelfTry, out targetShelf, out targetScreen))
            {
                Log("clickable empty wall missing");
                EnterStep(14);
                return;
            }

            if (ClickAt(targetScreen))
            {
                Log("shelf click " + targetShelf.name);
                EnterStep(8);
            }

            return;
        }

        if (step == 8)
        {
            RepaintGame();
            if (ElapsedFrames() < 12)
            {
                return;
            }

            Shelf selected = CurrentAssignment();
            if (selected == targetShelf)
            {
                EnterStep(9);
                return;
            }

            shelfTry++;
            Log("shelf mismatch " + (selected != null ? selected.name : "none"));
            if (shelfTry >= 4)
            {
                EnterStep(14);
                return;
            }

            EnterStep(7);
            return;
        }

        if (step == 9)
        {
            RepaintGame();
            TMP_Dropdown dropdown = ProductDropdown();
            if (dropdown == null)
            {
                Log("dropdown missing");
                EnterStep(14);
                return;
            }

            if (!dropdown.IsExpanded)
            {
                ClickAt(ScreenOf(dropdown.GetComponent<RectTransform>()));
                return;
            }

            EnterStep(10);
            return;
        }

        if (step == 10)
        {
            RepaintGame();
            TMP_Dropdown dropdown = ProductDropdown();
            if (dropdown == null)
            {
                Log("dropdown missing");
                EnterStep(14);
                return;
            }

            string caption = dropdown.captionText != null ? dropdown.captionText.text : "";
            if (caption.IndexOf("오리지널", StringComparison.Ordinal) >= 0)
            {
                Log("caption " + caption);
                EnterStep(11);
                return;
            }

            if (!dropdown.IsExpanded)
            {
                ClickAt(ScreenOf(dropdown.GetComponent<RectTransform>()));
                return;
            }

            if (!ClickDropdownOption("오리지널") && ElapsedFrames() > 160)
            {
                Log("chips option missing caption " + caption);
                EnterStep(14);
            }

            return;
        }

        if (step == 11)
        {
            RepaintGame();
            TMP_Dropdown dropdown = ProductDropdown();
            if (dropdown != null && dropdown.IsExpanded && ElapsedFrames() < 30)
            {
                return;
            }

            var build = UnityEngine.Object.FindAnyObjectByType<BuildModeController>();
            string productId = targetShelf != null && targetShelf.AssignedProduct != null ? targetShelf.AssignedProduct.ProductId : "";
            if (productId == "chips_original")
            {
                Log("assigned " + productId);
                EnterStep(12);
                return;
            }

            if (!acted && ElapsedFrames() > 20)
            {
                if (ClickButton(build, "applyProductButton"))
                {
                    acted = true;
                }
            }

            if (ElapsedFrames() > 80)
            {
                Log("assign failed " + productId);
                EnterStep(12);
            }

            return;
        }

        if (step == 12)
        {
            RepaintGame();
            if (ElapsedFrames() < 20)
            {
                return;
            }

            if (targetShelf != null && targetShelf.CurrentQuantity > 0)
            {
                Log("stocked " + targetShelf.CurrentQuantity);
                EnterStep(13);
                return;
            }

            if (!acted)
            {
                if (ClickButton(Hud(), "restockShelvesButton"))
                {
                    acted = true;
                }
            }

            if (ElapsedFrames() > 70)
            {
                Log("restock qty " + (targetShelf != null ? targetShelf.CurrentQuantity : -1));
                EnterStep(13);
            }

            return;
        }

        if (step == 13)
        {
            RepaintGame();
            if (ElapsedFrames() < 110)
            {
                return;
            }

            var build = UnityEngine.Object.FindAnyObjectByType<BuildModeController>();
            if (PanelActive(build, "buildPanel") || PanelActive(build, "productAssignmentPanel"))
            {
                ClickButton(build, "exitButton");
                return;
            }

            EnterStep(14);
            return;
        }

        if (step == 14)
        {
            RepaintGame();
            if (ElapsedFrames() >= 90)
            {
                StopMovie();
                BeginProbe("07_AssignRestock_R2");
                EnterStep(15);
            }

            return;
        }

        if (PollProbe(false))
        {
            NextCut();
        }
    }


    static void CutExpanded()
    {
        if (step == 0)
        {
            BeginCut();
            PrepareView();
            EnsureDevices();
            StockStore(2, 400000, true);
            var session = UnityEngine.Object.FindAnyObjectByType<StoreSession>();
            session.StartBusiness();
            session.SetSpeed(StoreSession.DoubleTimeScale);
            LogHud("expanded");
            EnterStep(1);
            return;
        }

        if (step == 1)
        {
            if (CustomerCount() >= 2 || ElapsedRealtime() > 25d)
            {
                if (!StartMovie("08_ExpandedStore_R2"))
                {
                    Fail();
                    return;
                }

                EnterStep(2);
            }

            return;
        }

        if (step == 2)
        {
            RepaintGame();
            if (ElapsedFrames() >= 9 * 60)
            {
                StopMovie();
                BeginProbe("08_ExpandedStore_R2");
                EnterStep(3);
            }

            return;
        }

        if (PollProbe(false))
        {
            NextCut();
        }
    }

    static void CutBuild()
    {
        if (step == 0)
        {
            BeginCut();
            PrepareView();
            EnsureDevices();
            var progression = UnityEngine.Object.FindAnyObjectByType<StoreProgression>();
            progression.TryRestoreCompletedStage(3);
            var economy = UnityEngine.Object.FindAnyObjectByType<StoreEconomy>();
            economy.TryRestoreState(250000, 0, 0);
            AlignDay(3);
            shelvesBefore = ActiveShelfCount();
            placeCells = null;
            placeIndex = 0;
            foundPlace = false;
            bestPlaceScore = float.MaxValue;
            LogHud("build");
            EnterStep(1);
            return;
        }

        if (step == 1)
        {
            if (ElapsedRealtime() < 0.4d)
            {
                return;
            }

            var build = UnityEngine.Object.FindAnyObjectByType<BuildModeController>();
            if (!PanelActive(build, "buildPanel"))
            {
                ClickButton(build, "buildButton");
                return;
            }

            EnterStep(2);
            return;
        }

        if (step == 2)
        {
            if (ElapsedFrames() < 15)
            {
                return;
            }

            var build = UnityEngine.Object.FindAnyObjectByType<BuildModeController>();
            if (ClickButton(build, "shelfButton"))
            {
                EnterStep(3);
            }

            return;
        }

        if (step == 3)
        {
            if (placeCells == null)
            {
                placeCells = CollectPlaceCells();
                placeIndex = 0;
                foundPlace = false;
                bestPlaceScore = float.MaxValue;
                markerFrame = Time.frameCount;
                Log("place cells " + placeCells.Count);
            }

            if (placeIndex >= placeCells.Count)
            {
                Log(foundPlace ? "chosen cell score " + bestPlaceScore : "no valid cell");
                EnterStep(4);
                return;
            }

            Vector3 world = placeCells[placeIndex];
            SetMouse(ScreenOfWorld(world), false);
            if (ElapsedFrames() < 6)
            {
                return;
            }

            if (PreviewIsValid())
            {
                Vector2 screen = ScreenOfWorld(world);
                float dx = screen.x - 820f;
                float dy = screen.y - 540f;
                float score = (dx * dx) + (dy * dy);
                if (!foundPlace || score < bestPlaceScore)
                {
                    foundPlace = true;
                    bestPlaceScore = score;
                    placePoint = world;
                    Log("candidate " + placeIndex + " " + screen);
                }
            }

            placeIndex++;
            markerFrame = Time.frameCount;
            return;
        }

        if (step == 4)
        {
            var build = UnityEngine.Object.FindAnyObjectByType<BuildModeController>();
            if (PanelActive(build, "buildPanel") && ElapsedFrames() < 40)
            {
                ClickButton(build, "exitButton");
                return;
            }

            if (!StartMovie("09_BuildUpgrade_R2"))
            {
                Fail();
                return;
            }

            EnterStep(5);
            return;
        }

        if (step == 5)
        {
            RepaintGame();
            if (ElapsedFrames() < 50)
            {
                return;
            }

            var build = UnityEngine.Object.FindAnyObjectByType<BuildModeController>();
            if (ClickButton(build, "buildButton"))
            {
                EnterStep(6);
            }

            return;
        }

        if (step == 6)
        {
            RepaintGame();
            if (ElapsedFrames() < 25)
            {
                return;
            }

            var build = UnityEngine.Object.FindAnyObjectByType<BuildModeController>();
            if (ClickButton(build, "shelfButton"))
            {
                EnterStep(7);
            }

            return;
        }

        if (step == 7)
        {
            RepaintGame();
            SetMouse(ScreenOfWorld(placePoint), false);
            if (ElapsedFrames() < 8)
            {
                return;
            }

            if (!PreviewIsValid())
            {
                if (ElapsedFrames() > 45)
                {
                    Log("preview lost");
                    EnterStep(9);
                }

                return;
            }

            if (ElapsedFrames() < 110)
            {
                return;
            }

            EnterStep(8);
            return;
        }

        if (step == 8)
        {
            RepaintGame();
            if (ClickAt(ScreenOfWorld(placePoint)))
            {
                Log("place click money " + Money());
                EnterStep(9);
            }

            return;
        }

        if (step == 9)
        {
            RepaintGame();
            var build = UnityEngine.Object.FindAnyObjectByType<BuildModeController>();
            if (!acted)
            {
                if (ElapsedFrames() < 10)
                {
                    return;
                }

                if (PanelActive(build, "buildPanel"))
                {
                    ClickButton(build, "exitButton");
                    return;
                }

                acted = true;
                markerFrame = Time.frameCount;
                Log("build clear money " + Money() + " shelves " + ActiveShelfCount() + " was " + shelvesBefore);
                return;
            }

            if (ElapsedFrames() < 130)
            {
                return;
            }

            if (ClickButton(Hud(), "upgradeButton"))
            {
                EnterStep(10);
            }

            return;
        }

        if (step == 10)
        {
            RepaintGame();
            if (ElapsedFrames() < 45)
            {
                return;
            }

            if (ClickExactLabel("구매"))
            {
                Log("upgrade clicked money " + Money());
                EnterStep(11);
            }
            else if (ElapsedFrames() > 90)
            {
                Log("upgrade button missing");
                EnterStep(11);
            }

            return;
        }

        if (step == 11)
        {
            RepaintGame();
            if (ElapsedFrames() >= 140)
            {
                StopMovie();
                BeginProbe("09_BuildUpgrade_R2");
                EnterStep(12);
            }

            return;
        }

        if (PollProbe(false))
        {
            NextCut();
        }
    }

    static void CutCampaign()
    {
        if (step == 0)
        {
            BeginCut();
            PrepareView();
            EnsureDevices();
            StockStore(4, 500000, true);
            skippedAhead = false;
            var session = UnityEngine.Object.FindAnyObjectByType<StoreSession>();
            session.StartBusiness();
            session.SetSpeed(StoreSession.TripleTimeScale);
            LogHud("campaign");
            Log("campaign day start");
            EnterStep(1);
            return;
        }

        if (step == 1)
        {
            RunOpenDay();
            var session = UnityEngine.Object.FindAnyObjectByType<StoreSession>();
            bool ready = session.Phase == StorePhase.Closing && CustomerCount() == 0;
            if (ready || ElapsedRealtime() > 340d)
            {
                Log("campaign ready revenue " + Revenue());
                if (!StartMovie("10_CampaignComplete_R2"))
                {
                    Fail();
                    return;
                }

                EnterStep(2);
            }

            return;
        }

        if (step == 2)
        {
            RepaintGame();
            if (ElapsedFrames() < 70)
            {
                return;
            }

            if (ClickButton(Hud(), "showResultButton"))
            {
                var progression = UnityEngine.Object.FindAnyObjectByType<StoreProgression>();
                Log("campaign result succeeded " + progression.LastSucceeded + " complete " + progression.ShowsCampaignComplete);
                EnterStep(3);
            }

            return;
        }

        if (step == 3)
        {
            RepaintGame();
            if (ElapsedFrames() < 240)
            {
                return;
            }

            if (ClickButton(Hud(), "nextDayButton"))
            {
                Log("continue stage " + UnityEngine.Object.FindAnyObjectByType<StoreProgression>().CompletedStage);
                EnterStep(4);
            }

            return;
        }

        if (step == 4)
        {
            RepaintGame();
            if (ElapsedFrames() == 30)
            {
                LogHud("sandbox");
            }

            if (ElapsedFrames() >= 180)
            {
                StopMovie();
                BeginProbe("10_CampaignComplete_R2");
                EnterStep(5);
            }

            return;
        }

        if (PollProbe(false))
        {
            NextCut();
        }
    }

    static void RunOpenDay()
    {
        var session = UnityEngine.Object.FindAnyObjectByType<StoreSession>();
        var progression = UnityEngine.Object.FindAnyObjectByType<StoreProgression>();
        if (session.Phase != StorePhase.Open)
        {
            return;
        }

        if (ElapsedRealtime() > 2d && Mathf.FloorToInt((float)ElapsedRealtime()) % 8 == 0)
        {
            RestockActive();
        }

        if (!skippedAhead && progression.TryGetDailyGoal(out int goal) && Revenue() >= goal)
        {
            session.JumpToSkipTime();
            skippedAhead = true;
            Log("skip after goal revenue " + Revenue());
        }
    }

    static void BeginCut()
    {
        step = 0;
        clickPhase = 0;
        keyPhase = 0;
        saleFrame = -1;
        cutStart = EditorApplication.timeSinceStartup;
        markerTime = cutStart;
        markerFrame = Time.frameCount;
        Application.runInBackground = true;
        Log("cut " + SessionState.GetInt(KeyCut, 0) + " screen " + Screen.width + "x" + Screen.height);
    }

    static void EnterStep(int next)
    {
        step = next;
        clickPhase = 0;
        keyPhase = 0;
        acted = false;
        stockBefore = -1;
        markerFrame = Time.frameCount;
        markerTime = EditorApplication.timeSinceStartup;
    }

    static bool ElapsedRealtime(double seconds)
    {
        return EditorApplication.timeSinceStartup - markerTime >= seconds;
    }

    static double ElapsedRealtime()
    {
        return EditorApplication.timeSinceStartup - markerTime;
    }

    static int ElapsedFrames()
    {
        return Time.frameCount - markerFrame;
    }

    static void EnterPlay()
    {
        PrepareView();
        if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = true;
        }
    }

    static void Finish()
    {
        SessionState.SetInt(KeyRun, 0);
        StopMovie();
        DestroyProbe();
        RestoreSave();
        Log("finished");
        WriteStatus();
    }

    static void Fail()
    {
        StopMovie();
        DestroyProbe();
        SessionState.SetInt(KeyRun, 0);
        RestoreSave();
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
        }

        Log("failed");
        WriteStatus();
    }

    static bool PollProbe(bool stopOnBlack)
    {
        if (probe == null)
        {
            Log("probe missing " + activeName);
            return true;
        }

        if (!probe.isPrepared)
        {
            if (ElapsedRealtime() > 12d)
            {
                Log("probe timeout " + probePath);
                DestroyProbe();
                if (stopOnBlack)
                {
                    Fail();
                    return false;
                }

                return true;
            }

            return false;
        }

        if (!probe.isPlaying && probe.frame < 30)
        {
            probe.Play();
            return false;
        }

        if (probe.texture == null || probe.frame < 30)
        {
            if (ElapsedRealtime() > 12d)
            {
                Log("probe no frame " + probePath);
                DestroyProbe();
                if (stopOnBlack)
                {
                    Fail();
                    return false;
                }

                return true;
            }

            return false;
        }

        int luma = SaveProbeFrame(out int frameWidth, out int frameHeight);
        double fps = probe.frameRate;
        double duration = fps > 0d ? probe.frameCount / fps : probe.length;
        long size = new FileInfo(probePath).Length;
        Log("file " + Path.GetFileName(probePath)
            + " bytes " + size
            + " " + frameWidth + "x" + frameHeight
            + " fps " + fps.ToString("0.##")
            + " duration " + duration.ToString("0.##")
            + " luma " + luma);
        bool black = luma < 12 || size < 100000 || frameWidth != Width || frameHeight != Height;
        DestroyProbe();
        if (black)
        {
            Log("invalid " + activeName);
            if (stopOnBlack)
            {
                Fail();
                return false;
            }
        }

        return true;
    }

    static void NextCut()
    {
        int cut = SessionState.GetInt(KeyCut, 0) + 1;
        SessionState.SetInt(KeyCut, cut);
        step = 0;
        Log("next cut index " + cut);
        EditorApplication.isPlaying = false;
    }

    static void BeginProbe(string fileName)
    {
        activeName = fileName;
        probePath = ResolveOutput(Path.Combine(OutputFolder(), fileName));
        if (string.IsNullOrEmpty(probePath))
        {
            Log("missing file " + fileName);
            return;
        }

        DestroyProbe();
        var host = new GameObject("RestockCaptureProbe");
        host.hideFlags = HideFlags.HideAndDontSave;
        probe = host.AddComponent<VideoPlayer>();
        probe.playOnAwake = false;
        probe.renderMode = VideoRenderMode.APIOnly;
        probe.audioOutputMode = VideoAudioOutputMode.None;
        probe.source = VideoSource.Url;
        probe.url = probePath;
        probe.errorReceived += (_, message) => Log("probe error " + message);
        probe.Prepare();
        markerTime = EditorApplication.timeSinceStartup;
    }

    static int SaveProbeFrame(out int frameWidth, out int frameHeight)
    {
        var renderTexture = probe.texture as RenderTexture;
        frameWidth = renderTexture != null ? renderTexture.width : (int)probe.width;
        frameHeight = renderTexture != null ? renderTexture.height : (int)probe.height;
        if (renderTexture == null)
        {
            return 0;
        }

        var previous = RenderTexture.active;
        RenderTexture.active = renderTexture;
        var texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0f, 0f, renderTexture.width, renderTexture.height), 0, 0);
        texture.Apply();
        RenderTexture.active = previous;
        File.WriteAllBytes(Path.Combine(OutputFolder(), activeName + "_frame.png"), texture.EncodeToPNG());
        int luma = AverageLuma(texture);
        UnityEngine.Object.Destroy(texture);
        return luma;
    }

    static int AverageLuma(Texture2D texture)
    {
        long total = 0;
        int count = 0;
        for (int y = 8; y < texture.height; y += 48)
        {
            for (int x = 8; x < texture.width; x += 48)
            {
                Color pixel = texture.GetPixel(x, y);
                total += (long)((pixel.r + pixel.g + pixel.b) * 85f);
                count++;
            }
        }

        return count == 0 ? 0 : (int)(total / count);
    }

    static void DestroyProbe()
    {
        if (probe == null)
        {
            return;
        }

        UnityEngine.Object.Destroy(probe.gameObject);
        probe = null;
    }

    static bool StartMovie(string fileName)
    {
        StopMovie();
        PrepareView();
        FocusGame();
        string folder = OutputFolder();
        Directory.CreateDirectory(folder);
        string output = Path.Combine(folder, fileName).Replace('\\', '/');
        string existing = output + ".mp4";
        if (File.Exists(existing))
        {
            File.Delete(existing);
        }

        var controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
        controller = new RecorderController(controllerSettings);
        var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        movie.name = fileName;
        movie.Enabled = true;
        movie.EncoderSettings = new CoreEncoderSettings
        {
            EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
            Codec = CoreEncoderSettings.OutputCodec.MP4
        };
        movie.ImageInputSettings = new GameViewInputSettings
        {
            OutputWidth = Width,
            OutputHeight = Height
        };
        movie.CaptureAlpha = false;
        movie.CaptureAudio = false;
        movie.OutputFile = output;
        controllerSettings.AddRecorderSettings(movie);
        controllerSettings.SetRecordModeToManual();
        controllerSettings.FrameRate = Rate;
        controllerSettings.FrameRatePlayback = FrameRatePlayback.Constant;
        controllerSettings.CapFrameRate = true;
        RecorderOptions.VerboseMode = true;
        controller.PrepareRecording();
        bool started = controller.StartRecording();
        Log("record " + fileName + " started " + started + " screen " + Screen.width + "x" + Screen.height);
        if (!started)
        {
            controller = null;
        }

        return started;
    }

    static void StopMovie()
    {
        if (controller == null)
        {
            return;
        }

        try
        {
            if (controller.IsRecording())
            {
                controller.StopRecording();
            }
        }
        catch (Exception exception)
        {
            Log("stop " + exception.Message);
        }

        controller = null;
    }

    static void StockStore(int stage, int money, bool fillEmpty)
    {
        var progression = UnityEngine.Object.FindAnyObjectByType<StoreProgression>();
        bool restored = progression.TryRestoreCompletedStage(stage);
        AlignDay(stage);
        var economy = UnityEngine.Object.FindAnyObjectByType<StoreEconomy>();
        economy.TryRestoreState(money, 0, 0);
        var inventory = UnityEngine.Object.FindAnyObjectByType<StoreInventory>();
        var ordering = FindInactive<StoreOrdering>();
        var products = new List<ProductDefinition>();
        for (int index = 0; index < inventory.ProductDefinitionCount; index++)
        {
            ProductDefinition product = inventory.GetProductDefinition(index);
            if (product == null || !progression.IsProductUnlocked(product))
            {
                continue;
            }

            products.Add(product);
            if (ordering != null)
            {
                ordering.TryOrder(product, 24);
            }
        }

        int assigned = 0;
        if (fillEmpty)
        {
            Shelf[] shelves = UnityEngine.Object.FindObjectsByType<Shelf>(FindObjectsInactive.Exclude);
            int cursor = 0;
            for (int index = 0; index < shelves.Length; index++)
            {
                Shelf shelf = shelves[index];
                if (!shelf.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (shelf.AssignedProduct != null)
                {
                    shelf.RestockToFull();
                    continue;
                }

                ProductDefinition pick = null;
                for (int attempt = 0; attempt < products.Count; attempt++)
                {
                    ProductDefinition candidate = products[(cursor + attempt) % products.Count];
                    if (candidate.StorageType != shelf.AcceptedStorageType)
                    {
                        continue;
                    }

                    pick = candidate;
                    cursor = (cursor + attempt + 1) % products.Count;
                    break;
                }

                if (pick != null && shelf.TryAssignProduct(pick))
                {
                    shelf.RestockToFull();
                    assigned++;
                }
            }
        }

        var session = UnityEngine.Object.FindAnyObjectByType<StoreSession>();
        Log("stock stage " + progression.CompletedStage + " day " + session.Day + " restored " + restored + " products " + products.Count + " assigned " + assigned + " money " + economy.CurrentMoney);
    }

    static bool AlignDay(int stage)
    {
        var session = UnityEngine.Object.FindAnyObjectByType<StoreSession>();
        if (session == null)
        {
            Log("session missing");
            return false;
        }

        int day = stage + 1;
        bool restored = session.TryRestorePreparationState(day);
        Log("align day " + session.Day + " for stage " + stage + " restored " + restored);
        return restored;
    }

    static void LogHud(string label)
    {
        var session = UnityEngine.Object.FindAnyObjectByType<StoreSession>();
        var progression = UnityEngine.Object.FindAnyObjectByType<StoreProgression>();
        int goal = 0;
        bool hasGoal = progression != null && progression.TryGetDailyGoal(out goal);
        Log(label
            + " day " + (session != null ? session.Day : -1)
            + " stage " + (progression != null ? progression.CompletedStage : -1)
            + " goal " + (hasGoal ? goal.ToString() : "none")
            + " campaign " + (progression != null && progression.ShowsCampaignComplete));
    }

    static void FrameStarterStore()
    {
        var controller = UnityEngine.Object.FindAnyObjectByType<IsometricCameraController>();
        if (controller == null)
        {
            Log("camera missing");
            return;
        }

        Camera camera = controller.GetComponent<Camera>();
        if (camera == null || !camera.orthographic)
        {
            Log("camera not orthographic");
            return;
        }

        var serialized = new SerializedObject(controller);
        float min = serialized.FindProperty("minOrthographicSize").floatValue;
        float max = serialized.FindProperty("maxOrthographicSize").floatValue;
        float lower = Mathf.Min(min, max);
        float upper = Mathf.Max(min, max);
        float zoomed = Mathf.Clamp(camera.orthographicSize - 2.5f, lower, upper);
        camera.orthographicSize = zoomed;
        Log("customer zoom " + camera.orthographicSize);
    }

    static void RestockActive()
    {
        Shelf[] shelves = UnityEngine.Object.FindObjectsByType<Shelf>(FindObjectsInactive.Exclude);
        for (int index = 0; index < shelves.Length; index++)
        {
            Shelf shelf = shelves[index];
            if (shelf.gameObject.activeInHierarchy && shelf.AssignedProduct != null)
            {
                shelf.RestockToFull();
            }
        }
    }

    static ProductDefinition FindProduct(string productId)
    {
        var inventory = UnityEngine.Object.FindAnyObjectByType<StoreInventory>();
        for (int index = 0; index < inventory.ProductDefinitionCount; index++)
        {
            ProductDefinition product = inventory.GetProductDefinition(index);
            if (product != null && product.ProductId == productId)
            {
                return product;
            }
        }

        return null;
    }

    static Shelf FindEmptyWall()
    {
        Shelf[] shelves = UnityEngine.Object.FindObjectsByType<Shelf>(FindObjectsInactive.Exclude);
        Shelf fallback = null;
        for (int index = 0; index < shelves.Length; index++)
        {
            Shelf shelf = shelves[index];
            if (!shelf.gameObject.activeInHierarchy || shelf.AssignedProduct != null || shelf.CurrentQuantity > 0)
            {
                continue;
            }

            if (shelf.AcceptedStorageType != ProductStorageType.Shelf)
            {
                continue;
            }

            if (fallback == null)
            {
                fallback = shelf;
            }

            if (shelf.name.IndexOf("Wall", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return shelf;
            }
        }

        return fallback;
    }

    static Vector3 ShelfCenter(Shelf shelf)
    {
        var renderer = shelf.GetComponentInChildren<Renderer>();
        return renderer != null ? renderer.bounds.center : shelf.transform.position;
    }

    static bool PanelActive(UnityEngine.Object owner, string fieldName)
    {
        if (owner == null)
        {
            return false;
        }

        var field = owner.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        var panel = field?.GetValue(owner) as GameObject;
        return panel != null && panel.activeInHierarchy;
    }

    static Shelf CurrentAssignment()
    {
        var build = UnityEngine.Object.FindAnyObjectByType<BuildModeController>();
        return typeof(BuildModeController).GetField("assignmentShelf", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(build) as Shelf;
    }

    static bool TryPickEmptyWall(int skip, out Shelf shelf, out Vector2 screen)
    {
        shelf = null;
        screen = default;
        int seen = 0;
        Shelf[] shelves = UnityEngine.Object.FindObjectsByType<Shelf>(FindObjectsInactive.Exclude);
        for (int index = 0; index < shelves.Length; index++)
        {
            Shelf candidate = shelves[index];
            if (!candidate.gameObject.activeInHierarchy || candidate.AssignedProduct != null || candidate.CurrentQuantity > 0)
            {
                continue;
            }

            if (candidate.AcceptedStorageType != ProductStorageType.Shelf)
            {
                continue;
            }

            if (candidate.name.IndexOf("Wall", StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            if (!TryShelfScreen(candidate, out Vector2 point))
            {
                continue;
            }

            if (seen == skip)
            {
                shelf = candidate;
                screen = point;
                return true;
            }

            seen++;
        }

        return false;
    }

    static bool TryShelfScreen(Shelf shelf, out Vector2 screen)
    {
        screen = default;
        Camera camera = Camera.main;
        if (camera == null || shelf == null)
        {
            return false;
        }

        var renderers = shelf.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return false;
        }

        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
        {
            bounds.Encapsulate(renderers[index].bounds);
        }

        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        Vector3[] samples =
        {
            center,
            center + new Vector3(0f, extents.y * 0.4f, 0f),
            center + new Vector3(extents.x * 0.35f, 0f, 0f),
            center + new Vector3(-extents.x * 0.35f, 0f, 0f),
            center + new Vector3(0f, 0f, extents.z * 0.35f),
            center + new Vector3(0f, 0f, -extents.z * 0.35f)
        };

        for (int index = 0; index < samples.Length; index++)
        {
            Vector3 projected = camera.WorldToScreenPoint(samples[index]);
            if (projected.z <= 0f)
            {
                continue;
            }

            var point = new Vector2(projected.x, projected.y);
            Ray ray = camera.ScreenPointToRay(point);
            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            if (hit.collider.GetComponentInParent<Shelf>() == shelf)
            {
                screen = point;
                return true;
            }
        }

        return false;
    }

    static bool ClickDropdownOption(string contains)
    {
        TMP_Dropdown dropdown = ProductDropdown();
        if (dropdown == null || !dropdown.IsExpanded)
        {
            return false;
        }

        Toggle[] toggles = dropdown.GetComponentsInChildren<Toggle>(false);
        for (int index = 0; index < toggles.Length; index++)
        {
            TMP_Text label = toggles[index].GetComponentInChildren<TMP_Text>();
            if (label == null || label.text == null || label.text.IndexOf(contains, StringComparison.Ordinal) < 0)
            {
                continue;
            }

            Vector2 screen = ScreenOf(label.rectTransform);
            if (!PointerHits(screen, toggles[index]))
            {
                var optionScroll = label.GetComponentInParent<ScrollRect>();
                if (optionScroll != null && (screen.y < 90f || screen.y > Screen.height - 90f))
                {
                    optionScroll.verticalNormalizedPosition = Mathf.Clamp01(optionScroll.verticalNormalizedPosition + (screen.y < 90f ? -0.25f : 0.25f));
                }

                return false;
            }

            return ClickAt(screen);
        }

        return false;
    }

    static bool ClickExactLabel(string exact)
    {
        TMP_Text[] labels = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude);
        for (int index = 0; index < labels.Length; index++)
        {
            TMP_Text label = labels[index];
            if (!label.gameObject.activeInHierarchy || label.text != exact)
            {
                continue;
            }

            var button = label.GetComponentInParent<Button>();
            if (button == null || !button.interactable || !button.gameObject.activeInHierarchy)
            {
                continue;
            }

            return ClickAt(ScreenOf(label.rectTransform));
        }

        return false;
    }

    static bool PreviewIsValid()
    {
        var build = UnityEngine.Object.FindAnyObjectByType<BuildModeController>();
        if (build == null)
        {
            return false;
        }

        var field = typeof(BuildModeController).GetField("previewValid", BindingFlags.Instance | BindingFlags.NonPublic);
        return field != null && (bool)field.GetValue(build);
    }

    static List<Vector3> CollectPlaceCells()
    {
        var cells = new List<Vector3>();
        var build = UnityEngine.Object.FindAnyObjectByType<BuildModeController>();
        if (build == null)
        {
            return cells;
        }

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var occupied = typeof(BuildModeController).GetField("occupiedCells", flags).GetValue(build) as HashSet<Vector2Int>;
        var blocked = typeof(BuildModeController).GetField("staticLayoutCells", flags).GetValue(build) as HashSet<Vector2Int>;
        if (occupied == null)
        {
            return cells;
        }

        float originX = (float)typeof(BuildModeController).GetField("originX", flags).GetValue(build);
        float originZ = (float)typeof(BuildModeController).GetField("originZ", flags).GetValue(build);
        float floorTop = (float)typeof(BuildModeController).GetField("floorTop", flags).GetValue(build);
        int gridWidth = (int)typeof(BuildModeController).GetField("gridWidth", flags).GetValue(build);
        int gridDepth = (int)typeof(BuildModeController).GetField("gridDepth", flags).GetValue(build);
        float cell = build.CellSize;
        for (int y = 1; y < gridDepth - 1; y++)
        {
            for (int x = 1; x < gridWidth - 2; x++)
            {
                var left = new Vector2Int(x, y);
                var right = new Vector2Int(x + 1, y);
                if (occupied.Contains(left) || occupied.Contains(right))
                {
                    continue;
                }

                if (blocked != null && (blocked.Contains(left) || blocked.Contains(right)))
                {
                    continue;
                }

                var world = new Vector3(originX + (x + 1f) * cell, floorTop, originZ + (y + 0.5f) * cell);
                Vector2 screen = ScreenOfWorld(world);
                if (screen.x < 430f || screen.y < 160f || screen.x > Screen.width - 80f || screen.y > Screen.height - 160f)
                {
                    continue;
                }

                cells.Add(world);
            }
        }

        return cells;
    }

    static bool TryPlacementPoint(out Vector3 world)
    {
        world = default;
        var build = UnityEngine.Object.FindAnyObjectByType<BuildModeController>();
        if (build == null)
        {
            return false;
        }

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var occupied = typeof(BuildModeController).GetField("occupiedCells", flags).GetValue(build) as HashSet<Vector2Int>;
        var blocked = typeof(BuildModeController).GetField("staticLayoutCells", flags).GetValue(build) as HashSet<Vector2Int>;
        if (occupied == null)
        {
            return false;
        }

        float originX = (float)typeof(BuildModeController).GetField("originX", flags).GetValue(build);
        float originZ = (float)typeof(BuildModeController).GetField("originZ", flags).GetValue(build);
        float floorTop = (float)typeof(BuildModeController).GetField("floorTop", flags).GetValue(build);
        int gridWidth = (int)typeof(BuildModeController).GetField("gridWidth", flags).GetValue(build);
        int gridDepth = (int)typeof(BuildModeController).GetField("gridDepth", flags).GetValue(build);
        float cell = build.CellSize;
        for (int y = 0; y < gridDepth; y++)
        {
            for (int x = 0; x < gridWidth - 1; x++)
            {
                var left = new Vector2Int(x, y);
                var right = new Vector2Int(x + 1, y);
                if (occupied.Contains(left) || occupied.Contains(right))
                {
                    continue;
                }

                if (blocked != null && (blocked.Contains(left) || blocked.Contains(right)))
                {
                    continue;
                }

                world = new Vector3(originX + (x + 1f) * cell, floorTop, originZ + (y + 0.5f) * cell);
                return true;
            }
        }

        return false;
    }

    static TMP_Dropdown ProductDropdown()
    {
        var build = UnityEngine.Object.FindAnyObjectByType<BuildModeController>();
        return typeof(BuildModeController).GetField("productDropdown", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(build) as TMP_Dropdown;
    }

    static bool ClickButton(UnityEngine.Object owner, string fieldName)
    {
        Button button = FieldButton(owner, fieldName);
        if (button == null || !button.gameObject.activeInHierarchy || !button.interactable)
        {
            if (ElapsedFrames() > 240 && clickPhase == 0)
            {
                Log("button unavailable " + fieldName);
                return true;
            }

            return false;
        }

        return ClickAt(ScreenOf(button.GetComponent<RectTransform>()));
    }

    static bool ClickText(string contains)
    {
        TMP_Text[] labels = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude);
        for (int index = 0; index < labels.Length; index++)
        {
            TMP_Text label = labels[index];
            if (!label.gameObject.activeInHierarchy || label.text == null || label.text.IndexOf(contains, StringComparison.Ordinal) < 0)
            {
                continue;
            }

            return ClickAt(ScreenOf(label.rectTransform));
        }

        return false;
    }

    static bool PointerHits(Vector2 screen, Component target)
    {
        if (EventSystem.current == null || target == null)
        {
            return false;
        }

        var data = new PointerEventData(EventSystem.current)
        {
            position = screen
        };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(data, hits);
        if (hits.Count == 0)
        {
            return false;
        }

        Transform hit = hits[0].gameObject.transform;
        return hit == target.transform || hit.IsChildOf(target.transform) || target.transform.IsChildOf(hit);
    }

    static bool ClickAt(Vector2 screen)
    {
        if (clickPhase == 0)
        {
            clickPos = screen;
            SetMouse(clickPos, false);
            clickPhase = 1;
            return false;
        }

        if (clickPhase == 1)
        {
            SetMouse(clickPos, true);
            clickPhase = 2;
            return false;
        }

        SetMouse(clickPos, false);
        clickPhase = 0;
        return true;
    }

    static void SetMouse(Vector2 screen, bool pressed)
    {
        if (Mouse.current == null)
        {
            return;
        }

        var state = new MouseState { position = screen };
        if (pressed)
        {
            state = state.WithButton(MouseButton.Left, true);
        }

        InputSystem.QueueStateEvent(Mouse.current, state);
    }

    static bool TapKey(Key key)
    {
        if (Keyboard.current == null)
        {
            return true;
        }

        if (keyPhase == 0)
        {
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key));
            keyPhase = 1;
            return false;
        }

        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        keyPhase = 0;
        return true;
    }

    static bool Reveal(RectTransform target, out Vector2 screen)
    {
        screen = ScreenOf(target);
        bool visible = screen.x > 40f && screen.x < Screen.width - 40f && screen.y > 40f && screen.y < Screen.height - 40f;
        if (visible)
        {
            return true;
        }

        var scroll = target.GetComponentInParent<ScrollRect>();
        if (scroll == null)
        {
            return true;
        }

        float delta = screen.y < 40f ? -0.1f : 0.1f;
        scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + delta);
        Canvas.ForceUpdateCanvases();
        screen = ScreenOf(target);
        return screen.x > 40f && screen.x < Screen.width - 40f && screen.y > 40f && screen.y < Screen.height - 40f;
    }

    static Vector2 ScreenOf(RectTransform rect)
    {
        var canvas = rect.GetComponentInParent<Canvas>();
        Camera camera = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            camera = canvas.worldCamera;
        }

        return RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
    }

    static Vector2 ScreenOfWorld(Vector3 world)
    {
        Vector3 screen = Camera.main.WorldToScreenPoint(world);
        return new Vector2(screen.x, screen.y);
    }

    static Button FirstOrderButton()
    {
        ProductOrderRow[] rows = UnityEngine.Object.FindObjectsByType<ProductOrderRow>(FindObjectsInactive.Include);
        for (int index = 0; index < rows.Length; index++)
        {
            var field = typeof(ProductOrderRow).GetField("orderButton", BindingFlags.Instance | BindingFlags.NonPublic);
            var button = field?.GetValue(rows[index]) as Button;
            if (button != null && button.gameObject.activeInHierarchy)
            {
                return button;
            }
        }

        return null;
    }

    static Button ChipsOrderButton()
    {
        ProductOrderRow[] rows = UnityEngine.Object.FindObjectsByType<ProductOrderRow>(FindObjectsInactive.Include);
        for (int index = 0; index < rows.Length; index++)
        {
            var productField = typeof(ProductOrderRow).GetField("product", BindingFlags.Instance | BindingFlags.NonPublic);
            var product = productField?.GetValue(rows[index]) as ProductDefinition;
            if (product == null || product.ProductId != "chips_original")
            {
                continue;
            }

            var buttonField = typeof(ProductOrderRow).GetField("orderButton", BindingFlags.Instance | BindingFlags.NonPublic);
            return buttonField?.GetValue(rows[index]) as Button;
        }

        return null;
    }

    static Button FieldButton(UnityEngine.Object owner, string fieldName)
    {
        if (owner == null)
        {
            return null;
        }

        var field = owner.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        return field?.GetValue(owner) as Button;
    }

    static StoreHud Hud()
    {
        return UnityEngine.Object.FindAnyObjectByType<StoreHud>();
    }

    static T FindInactive<T>() where T : UnityEngine.Object
    {
        T[] found = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include);
        return found.Length > 0 ? found[0] : null;
    }

    static int CustomerCount()
    {
        var spawner = UnityEngine.Object.FindAnyObjectByType<CustomerSpawner>();
        return spawner != null ? spawner.ActiveCustomerCount : 0;
    }

    static int Revenue()
    {
        var economy = UnityEngine.Object.FindAnyObjectByType<StoreEconomy>();
        return economy != null ? economy.DailyRevenue : 0;
    }

    static int Money()
    {
        var economy = UnityEngine.Object.FindAnyObjectByType<StoreEconomy>();
        return economy != null ? economy.CurrentMoney : 0;
    }

    static StorePhase Phase()
    {
        var session = UnityEngine.Object.FindAnyObjectByType<StoreSession>();
        return session != null ? session.Phase : StorePhase.Preparation;
    }

    static int ActiveShelfCount()
    {
        int count = 0;
        Shelf[] shelves = UnityEngine.Object.FindObjectsByType<Shelf>(FindObjectsInactive.Exclude);
        for (int index = 0; index < shelves.Length; index++)
        {
            if (shelves[index].gameObject.activeInHierarchy)
            {
                count++;
            }
        }

        return count;
    }

    static int FacilityCount()
    {
        return UnityEngine.Object.FindObjectsByType<Shelf>(FindObjectsInactive.Exclude).Length
            + UnityEngine.Object.FindObjectsByType<CheckoutCounter>(FindObjectsInactive.Exclude).Length;
    }

    static void EnsureDevices()
    {
        if (Mouse.current == null)
        {
            InputSystem.AddDevice<Mouse>();
        }

        if (Keyboard.current == null)
        {
            InputSystem.AddDevice<Keyboard>();
        }
    }

    static void PrepareView()
    {
        PlayModeWindow.SetViewType(PlayModeWindow.PlayModeViewTypes.GameView);
        PlayModeWindow.SetCustomRenderingResolution((uint)Width, (uint)Height, "Restock Capture");
        FocusGame();
    }

    static void FocusGame()
    {
        var type = Type.GetType("UnityEditor.GameView,UnityEditor");
        if (type == null)
        {
            return;
        }

        EditorWindow window = EditorWindow.GetWindow(type);
        window.Show();
        window.Focus();
        window.Repaint();
    }

    static void RepaintGame()
    {
        var type = Type.GetType("UnityEditor.GameView,UnityEditor");
        if (type == null)
        {
            return;
        }

        var window = EditorWindow.GetWindow(type);
        window.Repaint();
    }

    static bool PrepareScene()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path == "Assets/Scenes/Main.unity")
        {
            return true;
        }

        if (scene.isDirty)
        {
            Log("scene dirty " + scene.path);
            return false;
        }

        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        return true;
    }

    static bool PrepareSaveBackup()
    {
        string save = SavePath();
        if (!File.Exists(save))
        {
            Log("save missing " + save);
            return false;
        }

        string hash = Hash(save);
        Log("save before " + new FileInfo(save).Length + " " + hash);
        if (!string.Equals(hash, ExpectedSaveHash, StringComparison.OrdinalIgnoreCase))
        {
            Log("save hash mismatch");
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(BackupPath()));
        File.Copy(save, BackupPath(), true);
        return true;
    }

    static void RestoreSave()
    {
        string backup = BackupPath();
        string save = SavePath();
        if (!File.Exists(backup))
        {
            Log("restore missing backup");
            return;
        }

        File.Copy(backup, save, true);
        Log("save after " + new FileInfo(save).Length + " " + Hash(save));
    }

    static string SavePath()
    {
        return Path.Combine(Application.persistentDataPath, "restock_save.json");
    }

    static string BackupPath()
    {
        return Path.Combine(Path.GetTempPath(), "restock-tests", "audit", "save-backup-capture.json");
    }

    static string OutputFolder()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Recordings", "Restock"));
    }

    static string ResolveOutput(string basePath)
    {
        string exact = basePath + ".mp4";
        if (File.Exists(exact))
        {
            return exact;
        }

        string folder = Path.GetDirectoryName(basePath);
        string name = Path.GetFileName(basePath);
        if (!Directory.Exists(folder))
        {
            return null;
        }

        string[] files = Directory.GetFiles(folder, name + "*.mp4");
        string best = null;
        DateTime newest = DateTime.MinValue;
        for (int index = 0; index < files.Length; index++)
        {
            DateTime time = File.GetLastWriteTimeUtc(files[index]);
            if (time >= newest)
            {
                newest = time;
                best = files[index];
            }
        }

        return best;
    }

    static string Hash(string path)
    {
        using (var sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(path))
        {
            byte[] hash = sha.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "");
        }
    }

    static void WriteStatus()
    {
        try
        {
            Directory.CreateDirectory(OutputFolder());
            File.WriteAllText(
                Path.Combine(OutputFolder(), "capture-status.txt"),
                "run " + SessionState.GetInt(KeyRun, 0)
                + " cut " + SessionState.GetInt(KeyCut, 0)
                + " step " + step
                + " playing " + EditorApplication.isPlaying);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(exception.Message);
        }
    }

    static void Log(string message)
    {
        Debug.Log("RestockCapture: " + message);
        try
        {
            Directory.CreateDirectory(OutputFolder());
            File.AppendAllText(Path.Combine(OutputFolder(), "capture-log.txt"), DateTime.Now.ToString("HH:mm:ss") + " " + message + Environment.NewLine);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(exception.Message);
        }
    }
}
