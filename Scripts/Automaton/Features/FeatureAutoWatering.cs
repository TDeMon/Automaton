namespace CryoFall.Automaton.Features
{
    using System.Collections.Generic;
    using System.Linq;
    using AtomicTorch.CBND.CoreMod.Items.Tools;
    using AtomicTorch.CBND.CoreMod.StaticObjects.Loot;
    using AtomicTorch.CBND.CoreMod.StaticObjects.Vegetation.Plants;
    using AtomicTorch.CBND.CoreMod.Systems;
    using AtomicTorch.CBND.CoreMod.Systems.InteractionChecker;
    using AtomicTorch.CBND.CoreMod.Systems.Notifications;
    using AtomicTorch.CBND.CoreMod.Systems.Resources;
    using AtomicTorch.CBND.CoreMod.Systems.Watering;
    using AtomicTorch.CBND.GameApi.Data;
    using AtomicTorch.CBND.GameApi.Data.State;
    using AtomicTorch.CBND.GameApi.Data.World;
    using AtomicTorch.CBND.GameApi.Scripting;
    using AtomicTorch.CBND.GameApi.Scripting.ClientComponents;

    public class FeatureAutoWatering: ProtoFeatureWithInteractionQueue
    {
        public override string Name => "AutoWatering";

        public override string Description => "Water plants with the watering can in your hands.";

        private bool readyForInteraction = true;

        private IActionState lastActionState = null;

        protected override void PrepareFeature(List<IProtoEntity> entityList, List<IProtoEntity> requiredItemList)
        {
            entityList.AddRange(Api.FindProtoEntities<IProtoObjectPlant>());
            requiredItemList.AddRange(Api.FindProtoEntities<IProtoItemToolWateringCan>());
        }

        protected override void CheckInteractionQueue()
        {
            if (!readyForInteraction)
            {
                return;
            }

            // Remove from queue while it have object and they in our whitelist if:
            //  - object is destroyed
            //  - if object is container that we already have looted
            //  - if object not IProtoObjectGatherable
            //  - if we can not interact with object right now
            //  - if we can not gather anything from object
            while (interactionQueue.Count != 0 && EnabledEntityList.Contains(interactionQueue[0].ProtoGameObject) &&
                   (interactionQueue[0].IsDestroyed ||
                    (lastActionState != null &&
                     lastActionState.TargetWorldObject == interactionQueue[0] &&
                     lastActionState.IsCompleted &&
                     !lastActionState.IsCancelled &&
                     !lastActionState.IsCancelledByServer) ||
                    !(interactionQueue[0].ProtoGameObject is IProtoObjectPlant protoPlant) ||
                    !protoPlant.SharedCanInteract(CurrentCharacter, interactionQueue[0], false)))
            {
                interactionQueue.RemoveAt(0);
            }

            if (interactionQueue.Count == 0)
            {
                return;
            }

            var request = new ItemWorldActionRequest(CurrentCharacter, interactionQueue[0], SelectedItem);
            bool result = WateringSystem.Instance.SharedStartAction(request);

        }

        protected override bool TestObject(IStaticWorldObject staticWorldObject)
        {
            return staticWorldObject.ProtoGameObject is IProtoObjectPlant protoPlant &&
                   protoPlant.SharedCanInteract(CurrentCharacter, staticWorldObject, false) &&
                   SelectedItem?.ProtoItem is IProtoItemToolWateringCan wateringCan &&
                   wateringCan.SharedCanWater(SelectedItem) &&
                   !staticWorldObject.GetPublicState<PlantPublicState>().IsWatered;
        }

        /// <summary>
        /// Stop everything.
        /// </summary>
        public override void Stop()
        {
            if (interactionQueue?.Count > 0)
            {
                interactionQueue.Clear();
                InteractionCheckerSystem.CancelCurrentInteraction(CurrentCharacter);
            }
            readyForInteraction = true;
            lastActionState = null;
        }

        /// <summary>
        /// Setup any of subscriptions
        /// </summary>
        public override void SetupSubscriptions(ClientComponent parentComponent)
        {
            base.SetupSubscriptions(parentComponent);

            PrivateState.ClientSubscribe(
                s => s.CurrentActionState,
                OnActionStateChanged,
                parentComponent);
        }

        /// <summary>
        /// Init on component enabled.
        /// </summary>
        public override void Start(ClientComponent parentComponent)
        {
            base.Start(parentComponent);

            // Check if there an action in progress.
            if (PrivateState.CurrentActionState != null)
            {
                readyForInteraction = false;
                lastActionState = PrivateState.CurrentActionState;
            }

            // Check if we opened loot container before enabling component.
            var currentInteractionObject = InteractionCheckerSystem.SharedGetCurrentInteraction(CurrentCharacter);
            if (currentInteractionObject?.ProtoWorldObject is ProtoObjectLootContainer)
            {
                readyForInteraction = false;
            }
        }

        private void OnActionStateChanged()
        {
            if (PrivateState.CurrentActionState != null)
            {
                // Action was started.
                readyForInteraction = false;
                lastActionState = PrivateState.CurrentActionState;
            }
            else
            {
                readyForInteraction = true;
            }
        }
    }
}
