using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static PlasticPipe.Server.MonitorStats;


public class TurnStateEnemy : TurnBaseState
{
    public TurnStateEnemy(TurnManager context) : base(context) { }

    // Variables
    List<EC_Damage> _hostiles = new List<EC_Damage>();
    float _timeSinceAttack;

    public override void EnterState()
    {
        ArtifactManager.instance.TriggerStartOfEnemyTurn();
        _hostiles = DungeonManager.instance.CurrentRoom.GetHostiles();
        _timeSinceAttack = 0;

        // Reset healing flag for each new turn
        foreach (var enemy in _hostiles)
        {
            enemy.ResetHealingFlag(); // Make sure to reset this flag at the start of each turn
        }
    }

    public override void ExitState()
    {

    }

    public override void UpdateState()
    {
        // Check if cheat to disable enemies' turn is active
        if (GameCheats.EnemiesDisabled)
        {
            // Skip the enemy turn, go back to player turn
            SwitchState("Player");
            return;
        }

        if (_hostiles.Count > 0 && _timeSinceAttack >= _ctx.timeBetweenAttacks)
        {
            var enemy = _hostiles[0];

            //Check if the enemy has been destroyed or deactivated
            if (enemy == null || enemy.gameObject == null || !enemy.gameObject.activeInHierarchy)
            {
                Debug.LogWarning("Enemy was destroyed before acting. Skipping turn.");
                _hostiles.RemoveAt(0);
                _timeSinceAttack = 0;
                return;
            }

            if (enemy.WasDamaged)
            {
                // Heal node will execute healing
                BehaviorNode healAction = new HealNode(); // Heal first
                BehaviorNode healTree = new SequenceNode(new List<BehaviorNode>
                {
                   new IsEnemyDisabledNode(),   // Check if enemy should act
                   healAction                // Heal first
                });

                bool healResult = healTree.Execute(enemy); // Execute healing behavior
                if (healResult)
                {
                    Debug.Log("Enemy healed successfully.");
                }

                // Reset after healing, exit after healing
                _hostiles.RemoveAt(0);
                _timeSinceAttack = 0; // Reset the timer
                return;
            }


            // Randomly decide which action the enemy will take
            List<BehaviorNode> actionOptions = new List<BehaviorNode>
            {
               new AttackNode(),          // Attack once
               new AttackTwiceNode(),     // Attack twice
               new DefendNode(),          // Defend to reduce incoming damage
            };

            // Select a random action from the list
            int randIndex = UnityEngine.Random.Range(0, actionOptions.Count);
            BehaviorNode chosenAction = actionOptions[randIndex];

            // Create the behavior tree for the selected action
            BehaviorNode tree = new SequenceNode(new List<BehaviorNode>
            {
                 new IsEnemyDisabledNode(),   // Check if enemy should act
                 chosenAction                // Execute the randomly chosen action
            });

            // Execute the tree on the first hostile (enemy)
            bool result = tree.Execute(enemy);
            if (!result)
            {
                //Debug.Log("Enemy failed to act.");
            }

            // Remove the enemy from the list after performing the action
            _hostiles.RemoveAt(0);
            _timeSinceAttack = 0; // Reset the timer
        }

        // Exit combat if room is cleared
        if (DungeonManager.instance.CurrentRoom.Clear)
        {
            ArtifactManager.instance.TriggerClearRoom();
            SwitchState("Idle");
            return;
        }

        // Switch to Player turn if no hostiles are left
        if (_hostiles.Count <= 0)
        {
            SwitchState("Player");
            return;
        }

        // Increment attack timer
        _timeSinceAttack += Time.deltaTime;
    }
}