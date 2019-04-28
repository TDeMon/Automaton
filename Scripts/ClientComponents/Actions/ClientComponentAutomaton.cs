﻿namespace CryoFall.Automaton.ClientComponents.Actions
{
    using AtomicTorch.CBND.GameApi.Scripting.ClientComponents;
    using CryoFall.Automaton.UI.Controls.Core.Automaton.Features;
    using CryoFall.Automaton.UI.Controls.Core.Managers;
    using System.Collections.Generic;

    public class ClientComponentAutomaton : ClientComponent
    {
        private static ClientComponentAutomaton instance;

        public static double UpdateInterval => AutomatonManager.UpdateInterval;

        private double accumulatedTime = UpdateInterval;

        private Dictionary<string, ProtoFeature> featuresDictionary;

        public ClientComponentAutomaton()
        {
            featuresDictionary = AutomatonManager.GetFeaturesDictionary();
        }

        protected override void OnDisable()
        {
            ReleaseSubscriptions();
            if (ReferenceEquals(this, instance))
            {
                foreach (var feature in featuresDictionary.Values)
                {
                    feature.Stop();
                }
                instance = null;
            }
        }

        protected override void OnEnable()
        {
            instance = this;
            foreach (var feature in featuresDictionary.Values)
            {
                feature.Start(this);
            }
        }

        public override void Update(double deltaTime)
        {
            foreach (var feature in featuresDictionary.Values)
            {
                feature.Update(deltaTime);
            }

            accumulatedTime += deltaTime;
            if (accumulatedTime < UpdateInterval)
            {
                return;
            }
            this.accumulatedTime %= UpdateInterval;


            foreach (var feature in featuresDictionary.Values)
            {
                feature.Execute();
            }
        }
    }
}