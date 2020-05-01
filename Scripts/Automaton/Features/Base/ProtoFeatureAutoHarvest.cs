using CryoFall.Automaton.ClientSettings;
using CryoFall.Automaton.ClientSettings.Options;

namespace CryoFall.Automaton.Features
{
    using System;
    using System.Linq;
    using AtomicTorch.CBND.CoreMod;
    using AtomicTorch.CBND.CoreMod.Characters.Input;
    using AtomicTorch.CBND.CoreMod.Characters.Player;
    using AtomicTorch.CBND.CoreMod.CraftRecipes;
    using AtomicTorch.CBND.CoreMod.Items.Weapons;
    using AtomicTorch.CBND.CoreMod.Systems.Physics;
    using AtomicTorch.CBND.CoreMod.Systems.Weapons;
    using AtomicTorch.CBND.GameApi.Data.Items;
    using AtomicTorch.CBND.GameApi.Data.Physics;
    using AtomicTorch.CBND.GameApi.Data.World;
    using AtomicTorch.CBND.GameApi.Extensions;
    using AtomicTorch.CBND.GameApi.Scripting;
    using AtomicTorch.GameEngine.Common.Extensions;
    using AtomicTorch.GameEngine.Common.Primitives;
    using AtomicTorch.CBND.CoreMod.Systems.Notifications;


    public abstract class ProtoFeatureAutoHarvest: ProtoFeature
    {
        public virtual bool IsWalkingEnabled { get; set; }
        public virtual bool ShouldCheckVisibility { get; set; }

        public string IsWalkingEnabledText => "Walk to the nearest target";
        public string ShouldCheckVisibilityText => "Walk only to the visible target. Work in progress";

        private bool attackInProgress = false;

        private IStaticWorldObject rememberedTarget; // To keep going to it when line of sight is lost

        /// <summary>
        /// Called by client component every tick.
        /// </summary>
        public override void Update(double deltaTime)
        {
            if (!(IsEnabled && CheckPrecondition()))
            {
                Stop();
                return;
            }
        }

        /// <summary>
        /// Called by client component on specific time interval.
        /// </summary>
        public override void Execute( )
        {
            if (!(IsEnabled && CheckPrecondition()))
            {
                return;
            }

            if (!attackInProgress)
            {
                FindAndAttackTarget();
            }
        }

        protected virtual bool AdditionalValidation(IStaticWorldObject testWorldObject)
        {
            return true;
        }

        protected virtual bool ShouldCurrentWeaponTriggerMovement(IStaticWorldObject testWorldObject)
        {
            return true;
        }

        public override void PrepareOptions(SettingsFeature settingsFeature)
        {
            // Full override of default settings because we need to change order of some options.
            AddOptionIsEnabled(settingsFeature);
            Options.Add(new OptionSeparator());
            Options.Add(new OptionCheckBox(
                parentSettings: settingsFeature,
                id: "IsWalkingEnabled",
                label: IsWalkingEnabledText,
                defaultValue: true,
                valueChangedCallback: value =>
                {
                    IsWalkingEnabled = value;
                }));
            Options.Add(new OptionCheckBox(
                parentSettings: settingsFeature,
                id: "ShouldCheckVisibility",
                label: ShouldCheckVisibilityText,
                defaultValue: true,
                valueChangedCallback: value =>
                {
                    ShouldCheckVisibility = value;
                }));
            Options.Add(new OptionSeparator());
            AddOptionEntityList(settingsFeature);
        }

        private IStaticWorldObject FindTarget(Vector2D weaponPos)
        {
            double searchDistance = GetCurrentWeaponRange() * 25;
            if (rememberedTarget != null && !rememberedTarget.IsDestroyed && rememberedTarget.PhysicsBody.Position.DistanceTo(weaponPos) < searchDistance)
            {
                return rememberedTarget;
            }

            
            using var objectsVisible = this.CurrentCharacter.PhysicsBody.PhysicsSpace
                                          .TestCircle(position: weaponPos,
                                                      radius: searchDistance, // I don't know what units the game uses, so let's stick with the search radius ~25 times as far as the weapon can hit.
                                                      collisionGroup: CollisionGroups.HitboxMelee);

            // I'd rather chain these methods, but it seems like `using` has some relation to IDisposable. I'm not sure of consequences not using it might cause, so I'll copy the code. It's not like I want to keep your memory free from leaks or anything.
            var sortedVisibleObjects = objectsVisible?.AsList()
                                          ?.Where(t => this.EnabledEntityList.Contains(t.PhysicsBody?.AssociatedWorldObject?.ProtoGameObject))
                                          ?.Where(t => t.PhysicsBody?.AssociatedWorldObject is IStaticWorldObject)
                                          ?.Where(t => this.AdditionalValidation(t.PhysicsBody?.AssociatedWorldObject as IStaticWorldObject))
                                          ?.Where(t => this.CheckIsVisible(t.PhysicsBody, weaponPos))
                                          ?.OrderBy(obj => obj.PhysicsBody.Position.DistanceTo(weaponPos))
                                          ?.ToList();
            if (sortedVisibleObjects == null || sortedVisibleObjects.Count == 0)
            {
                return null;
            }

            rememberedTarget = sortedVisibleObjects[0].PhysicsBody.AssociatedWorldObject as IStaticWorldObject; // NPE checked somewhere in the stream above
            return rememberedTarget;
        }

        private void FindAndAttackTarget( )
        {
            var fromPos = CurrentCharacter.Position; // or + GetWeaponOffset()?

            IStaticWorldObject target = FindTarget(fromPos);
            if (target == null)
            {
                return;
            }

            bool canAlreadyHit = GetCenterPosition(target.PhysicsBody).DistanceTo(fromPos) < this.GetCurrentWeaponRange() / 1.1; // Get a bit closer to the target than maximal range.
            if (!canAlreadyHit)
            {
                MoveToClosestTarget(fromPos, target);
                return; // Can safely ignore code below, because if we have to move to the closest object to attack it, we definitely cannot hit it.
            }


            var targetPoint = GetCenterPosition(target.PhysicsBody);
            if (this.CheckForObstacles(target, targetPoint, GetCurrentWeaponRange()))
            {
                this.AttackTarget(target, targetPoint);
                this.attackInProgress = true;
                ClientTimersSystem.AddAction(this.GetCurrentWeaponAttackDelay(), () =>
                                                                                    {
                                                                                        if (this.attackInProgress)
                                                                                        {
                                                                                            this.attackInProgress = false;
                                                                                            this.StopItemUse();
                                                                                            this.FindAndAttackTarget();
                                                                                        }
                                                                                    });
                return;
            }
          
        }

        public void AttackTarget(IWorldObject targetObject, Vector2D intersectionPoint)
        {
            if (targetObject == null)
            {
                return;
            }

            var deltaPositionToMouseCursor = CurrentCharacter.Position +
                                             GetWeaponOffset() -
                                             intersectionPoint;
            var rotationAngleRad =
                Math.Abs(Math.PI + Math.Atan2(deltaPositionToMouseCursor.Y, deltaPositionToMouseCursor.X));
            var moveModes = PlayerCharacter.GetPrivateState(CurrentCharacter).Input.MoveModes;
            // TODO: don't prevent moving
            var command = new CharacterInputUpdate(moveModes, (float)rotationAngleRad);
            ((PlayerCharacter)CurrentCharacter.ProtoCharacter).ClientSetInput(command);
            // TODO: prevent user mousemove to interrupt it
            SelectedItem.ProtoItem.ClientItemUseStart(SelectedItem);
        }

        public void MoveToClosestTarget(Vector2D weaponPos, IWorldObject targetObject)
        {
            if (!IsWalkingEnabled) return;

            Vector2D target = GetCenterPosition(targetObject.PhysicsBody);
            //NotificationSystem.ClientShowNotification("" + target);
            Vector2D diff = target - weaponPos;

            var moveModes = CharacterMoveModesHelper.CalculateMoveModes(diff) | CharacterMoveModes.ModifierRun; // Running will yield us more LP/minute (by miniscule amount, though)
            var command = new CharacterInputUpdate(moveModes, 0); // Ugh, too lazy to look for usages to understand whether `0` is "up" or "right". Probably "right", but I won't mess with trigonometry, forgive me.
            ((PlayerCharacter)CurrentCharacter.ProtoCharacter).ClientSetInput(command);

        }

        protected virtual double GetCurrentWeaponRange()
        {
            if(SelectedItem.ProtoItem is IProtoItemWeaponMelee toolItem &&
               toolItem.OverrideDamageDescription != null)
            {
                return toolItem.OverrideDamageDescription.RangeMax;
            }
            Api.Logger.Error("Automaton: OverrideDamageDescription is null for " + SelectedItem);
            return 0d;
        }

        protected Vector2D GetWeaponOffset()
        {
            return new Vector2D(0, CurrentCharacter.ProtoCharacter.CharacterWorldWeaponOffsetMelee);
        }

        protected double GetCurrentWeaponAttackDelay()
        {
            var toolItem = SelectedItem.ProtoItem as IProtoItemWeaponMelee;
            return toolItem?.FireInterval ?? 0d;
        }

        // Returns true if not obscured
        private bool CheckIsVisible(IPhysicsBody targetObject, Vector2D weaponPos)
        {
            if (!ShouldCheckVisibility) return true;

            using var raycast = CurrentCharacter.PhysicsBody.PhysicsSpace.TestLine(
                fromPosition: weaponPos,
                toPosition: GetCenterPosition(targetObject),
                collisionGroup: CollisionGroups.HitboxMelee
                );

            var visible = raycast?.AsList()
                ?.Where(t => t.PhysicsBody?.AssociatedWorldObject is IStaticWorldObject) // Otherwise it will 99% times hit an item in your hands. For example, an axe.
                ?.ToList(); 

            if (visible == null || visible.Count == 0) return false;

            var first = visible.ElementAt(0);

            return first.PhysicsBody == targetObject;
        }

        private Vector2D GetCenterPosition(IPhysicsBody target)
        {
            var shape = target.Shapes.FirstOrDefault(s => s.CollisionGroup == CollisionGroups.HitboxMelee);
            if (shape == null)
            {
                Api.Logger.Error("Automaton: target object has no HitBoxMelee shape " + target);
                return target.Position; 
            }
            return this.ShapeCenter(shape) + target.Position;
        }


        // Returns true if not obscured
        private bool CheckForObstacles(IWorldObject targetObject, Vector2D intersectionPoint, double maxDistance)
        {
            // Check for obstacles in line between character and object
            var fromPos = CurrentCharacter.Position + GetWeaponOffset();
            // Normalize vector and set it length to weapon range
            var toPos = (fromPos - intersectionPoint).Normalized * maxDistance;
            // Check if in range
            bool canReachObject = false;
            using var obstaclesOnTheWay = this.CurrentCharacter.PhysicsBody.PhysicsSpace
                                              .TestLine(fromPosition: fromPos,
                                                        toPosition: fromPos - toPos,
                                                        collisionGroup: CollisionGroups.HitboxMelee);
            foreach (var testResult in obstaclesOnTheWay.AsList())
            {
                var testResultPhysicsBody = testResult.PhysicsBody;
                if (testResultPhysicsBody.AssociatedProtoTile != null)
                {
                    if (testResultPhysicsBody.AssociatedProtoTile.Kind != TileKind.Solid)
                    {
                        // non-solid obstacle - skip
                        continue;
                    }
                    // tile on the way - blocking damage ray
                    break;
                }

                var testWorldObject = testResultPhysicsBody.AssociatedWorldObject;
                if (testWorldObject == this.CurrentCharacter)
                {
                    // ignore collision with self
                    continue;
                }

                if (!(testWorldObject.ProtoGameObject is IDamageableProtoWorldObject))
                {
                    // shoot through this object
                    continue;
                }

                if (testWorldObject == targetObject)
                {
                    canReachObject = true;
                    continue;
                }

                if (this.EnabledEntityList.Contains(testWorldObject.ProtoWorldObject))
                {
                    // Another object to harvest in line - fire it anyway
                    continue;
                }
                // another object on the way
                return false;
            }

            return canReachObject;
        }

        private Vector2D ShapeCenter(IPhysicsShape shape)
        {
            if (shape != null)
            {
                switch (shape.ShapeType)
                {
                    case ShapeType.Rectangle:
                        var shapeRectangle = (RectangleShape)shape;
                        return shapeRectangle.Position + shapeRectangle.Size / 2d;
                    case ShapeType.Point:
                        var shapePoint = (PointShape)shape;
                        return shapePoint.Point;
                    case ShapeType.Circle:
                        var shapeCircle = (CircleShape)shape;
                        return shapeCircle.Center;
                    case ShapeType.Line:
                        break;
                    case ShapeType.LineSegment:
                        var lineSegmentShape = (LineSegmentShape)shape;
                        return new Vector2D((lineSegmentShape.Point1.X + lineSegmentShape.Point2.X) / 2d,
                                     (lineSegmentShape.Point1.Y + lineSegmentShape.Point2.Y) / 2d);
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            return new Vector2D(0, 0);
        }

        private void StopItemUse()
        {
            SelectedItem?.ProtoItem.ClientItemUseFinish(SelectedItem);
        }

        /// <summary>
        /// Stop everything.
        /// </summary>
        public override void Stop()
        {
            if (attackInProgress)
            {
                attackInProgress = false;
                StopItemUse();
            }
            rememberedTarget = null;
        }
    }
}
