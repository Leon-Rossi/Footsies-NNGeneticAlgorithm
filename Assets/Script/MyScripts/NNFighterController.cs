using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Footsies;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.XR;

public class NNFighterController : MonoBehaviour
{
    AIControl aiControl;
    NeuralNetworkController neuralNetworkController;
    BattleCore battleCore;
    SelectNN selectNN;

    int currentAISave;

    List<List<List<List<List<float>>>>> NNList = new List<List<List<List<List<float>>>>>();

    int NNLeft;
    int NNRight;
    int maxFightPerCapita = 4;

    float leftTimeSinceNoAttack;
    float rightTimeSinceNoAttack;

    int listRun;
    int listPos;

    bool isVsNN;
    private int rightLastOutputResult;
    private int leftLastOutputResult;


    // Start is called before the first frame update
    void Awake()
    {
        aiControl = GameObject.Find("GameMaster").GetComponent<AIControl>();
        neuralNetworkController = GameObject.Find("GameMaster").GetComponent<NeuralNetworkController>();
        battleCore = GameObject.Find("BattleCore").GetComponent<BattleCore>();
        selectNN = GameObject.Find("MenuManager").GetComponent<SelectNN>();

        currentAISave = aiControl.currentAISave;
        NNList = aiControl.AISaves[currentAISave].GiveLastGeneration();

        listPos = 1;
        listRun = 0;
        NNRight = 0;

        float smallestValue = 10000;
        for(int i = 0; i <= NNList.Count -1; i++)
        {
            if(Math.Abs(NNList[NNRight][0][0][0][1] - NNList[i][0][0][0][1]) < smallestValue && NNRight != i)
            {
                NNLeft = i;
                smallestValue = NNList[NNRight][0][0][0][1] - NNList[i][0][0][0][1];
            }
        }

        if(GameManager.Instance.isVsNN)
        {
            NNList = aiControl.AISaves[selectNN.selectedSafe].bestNeuralNetworks;
            isVsNN = true;
            NNRight = selectNN.generation;
            if(NNRight == -1)
            {
                NNRight = NNList.Count - 1;
            }
        }

        Time.timeScale = aiControl.speed;
    }

    public void NextNNDuel(bool rightFighterWon = false, bool draw = false, bool winByGuard = false)
    {
        int gameValue = winByGuard ? 3 : 30;

        float WinExpectedRight = (float)(1/(1+ Math.Pow(10, (NNList[NNLeft][0][0][0][1] - NNList[NNRight][0][0][0][1])/ 400)));
        float WinExpectedLeft = (float)(1/(1+ Math.Pow(10, (NNList[NNRight][0][0][0][1] - NNList[NNLeft][0][0][0][1])/ 400)));

        if(!draw)
        {
            
            if(rightFighterWon)
            {
                NNList[NNRight][0][0][0][1] += gameValue * (1-WinExpectedRight);
                NNList[NNLeft][0][0][0][1] += gameValue * (0-WinExpectedLeft);
            }
            else
            {
                NNList[NNRight][0][0][0][1] += gameValue * (0-WinExpectedRight);
                NNList[NNLeft][0][0][0][1] += gameValue * (1-WinExpectedLeft);
            }
        }
        else
        {
            NNList[NNRight][0][0][0][1] += gameValue * (0.5f-WinExpectedRight);
            NNList[NNLeft][0][0][0][1] += gameValue * (0.5f-WinExpectedLeft);
        }

        if(listRun <= maxFightPerCapita -1)
        {
            NNRight = listPos;
            while(NNList[NNRight][0][0][0][1] < 50)
            {
                listPos++;
                
                if(listPos >= NNList.Count)
                {
                    listPos = 0;
                    listRun ++;

                    if(listRun <= maxFightPerCapita -1)
                    {
                        StartNextGeneration();
                        listPos = 0;
                        listRun = 0;
                        return;
                    }
                }

                NNRight = listPos;
            }

            float smallestValue = 10000;
            for(int i = 0; i <= NNList.Count -1; i++)
            {
                if(Math.Abs(NNList[NNRight][0][0][0][1] - NNList[i][0][0][0][1]) < smallestValue && NNRight != i)
                {
                    NNLeft = i;
                    smallestValue = Math.Abs(NNList[NNRight][0][0][0][1] - NNList[i][0][0][0][1]);
                }
            }

            listPos ++;
            if(listPos >= NNList.Count)
            {
                listPos = 0;
                listRun ++;
            }
        }
        else
        {
            StartNextGeneration();
            listPos = 0;
            listRun = 0;
        }
    }

    void StartNextGeneration()
    {
        aiControl.AISaves[currentAISave].ReplaceLastGeneration(NNList);
        aiControl.AISaves[currentAISave].SetUpNextGeneration();
        NNList = aiControl.AISaves[currentAISave].GiveLastGeneration();

        aiControl.SaveFile();
    }

    public int RunRightNN()
    {
        bool attack = false;
        bool moveRight = false;
        bool moveLeft = false;

        int inputData = 0;

        List<float> output = neuralNetworkController.RunNN(NNList[NNRight], GetInput(false), NeuralNetworkController.ActivationFunctions.Sigmoid);
        
        //Add outputs as Fighter Inputs
        if(output[0] > 0.5)
        {
            attack = true;
            rightTimeSinceNoAttack ++;
        }
        else
        {
            rightTimeSinceNoAttack = 0;
        }

        if(output[1] < 0.4)
        {
            moveLeft = true;
        }
        else if(output[1] > 0.6)
        {
            moveRight = true;
        }

        inputData |= attack ? (int)InputDefine.Attack : 0;
        inputData |= moveLeft ? (int)InputDefine.Left : 0;
        inputData |= moveRight ? (int)InputDefine.Right : 0;
        
        rightLastOutputResult = inputData;

        return inputData;
    }

    public int RunLeftNN()
    {
        bool attack = false;
        bool moveRight = false;
        bool moveLeft = false;

        int inputData = 0;

        List<float> output = neuralNetworkController.RunNN(NNList[NNLeft], GetInput(true), NeuralNetworkController.ActivationFunctions.Sigmoid);
        
        //Add outputs as Fighter Inputs
        if(output[0] > 0.5)
        {
            attack = true;
            leftTimeSinceNoAttack ++;
        }
        else
        {
            leftTimeSinceNoAttack = 0;
        }

        if(output[1] > 0.6)
        {
            moveLeft = true;
        }
        else if(output[1] < 0.4)
        {
            moveRight = true;
        }

        inputData |= attack ? (int)InputDefine.Attack : 0;
        inputData |= moveLeft ? (int)InputDefine.Left : 0;
        inputData |= moveRight ? (int)InputDefine.Right : 0;

        leftLastOutputResult = inputData;
        
        return inputData;
    }

    public List<float> GetInput(bool isLeftFighter)
    {
        List<float> leftInfo = new List<float>(){
        leftLastOutputResult == 1 || leftLastOutputResult == 5? 1 : 0,
        leftLastOutputResult == 2 || leftLastOutputResult == 6? 1 : 0,
        leftLastOutputResult >= 4 ? 1 : 0,
        leftLastOutputResult == 7 || leftLastOutputResult == 8? 1 : 0,

        Math.Clamp(leftTimeSinceNoAttack /100, 0, 1),

        battleCore.fighter1.guardHealth == 0 ? 1 : 0,
        battleCore.fighter1.currentHitStunFrame != 0 ? 1 : 0,
        battleCore.fighter1.isAlwaysCancelable? 1 : (float)battleCore.fighter1.currentActionFrame / (float)battleCore.fighter1.currentActionFrameCount,
        };

        List<float> rightInfo = new List<float>(){
        rightLastOutputResult == 1 || rightLastOutputResult == 5? 1 : 0,
        rightLastOutputResult == 2 || rightLastOutputResult == 6? 1 : 0,
        rightLastOutputResult >= 4 ? 1 : 0,
        rightLastOutputResult == 7 || rightLastOutputResult == 8? 1 : 0,

        Math.Clamp(rightTimeSinceNoAttack /100, 0, 1),

        battleCore.fighter2.guardHealth == 0 ? 1 : 0,
        battleCore.fighter2.currentHitStunFrame != 0 ? 1 : 0,
        battleCore.fighter2.isAlwaysCancelable? 1 : (float)battleCore.fighter2.currentActionFrame / (float)battleCore.fighter2.currentActionFrameCount,
        };

        List<float> additionalInfo = new List<float>(){
            Math.Abs(battleCore.fighter1.position.x - battleCore.fighter2.position.x),
            battleCore.GetFrameAdvantage(isLeftFighter),
        };

        if(isLeftFighter)
        {
            leftInfo.AddRange(rightInfo);
            leftInfo.AddRange(additionalInfo);

            string stringOut = "Inputs: ";
            leftInfo.ForEach(x => stringOut += x + " ");

            return leftInfo;
        }
        rightInfo.AddRange(leftInfo);
        rightInfo.AddRange(additionalInfo);
        return rightInfo;
    }
}