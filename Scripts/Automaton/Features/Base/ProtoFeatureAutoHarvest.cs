﻿namespace CryoFall.Automaton.Features
{
    using System;
    using System.Linq;
    using AtomicTorch.CBND.CoreMod;
    using AtomicTorch.CBND.CoreMod.Characters.Input;
    using AtomicTorch.CBND.CoreMod.Characters.Player;
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

    public abstract class ProtoFeatureAutoHarvest: ProtoFeature
    {
        private bool attackInProgress = false;

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

        private void FindAndAttackTarget( )
        {
            var fromPos = CurrentCharacter.Position + GetWeaponOffset();
            
            using var objectsVisible = this.CurrentCharacter.PhysicsBody.PhysicsSpace
                                          .TestCircle(position: fromPos,
                                                      radius: this.GetCurrentWeaponRange() * 25, // I don't know what units the game uses, so let's stick with the search radius ~25 times as far as the weapon can hit.
                                                      collisionGroup: CollisionGroups.HitboxMelee);
                                          
            // I'd rather chain these methods, but it seems like `using` has some relation to IDisposable. I'm not sure of consequences not using it might cause, so I'll copy the code. It's not like I want to keep your memeory free from leaks or anything.
            var sortedVisibleObjects = objectsVisible?.AsList()
                                          ?.Where(t => this.EnabledEntityList.Contains(t.PhysicsBody?.AssociatedWorldObject?.ProtoGameObject))
                                          ?.Where(t => t.PhysicsBody?.AssociatedWorldObject is IStaticWorldObject)
                                          ?.Where(t => this.AdditionalValidation(t.PhysicsBody?.AssociatedWorldObject as IStaticWorldObject))
                                          ?.OrderBy(obj => obj.PhysicsBody.Position.DistanceTo(fromPos))
                                          ?.ToList();
            if (sortedVisibleObjects == null || sortedVisibleObjects.Count == 0)
            {
                return;
            }

            bool canAlreadyHit = sortedVisibleObjects[0].PhysicsBody.Position.DistanceTo(fromPos) < this.GetCurrentWeaponRange() / 2; // Get closer than maximal distance
            if (!canAlreadyHit)
            {
                MoveToClosestTarget(fromPos, sortedVisibleObjects[0].PhysicsBody.AssociatedWorldObject);
                return; // Can safely ignore code below, because if we have to move to the closest object to attack it, we definitely cannot hit it.
            }

            // Better to leave this code as is and not to link `objectOfInterest` to `sortedVisibleObjects`. There is a foreach loop below over a _single_ object, so that may mean there can be several decoratie objects within one PhysicsBody. Probably. Or two trees standing nearby.
            using var objectsNearby = this.CurrentCharacter.PhysicsBody.PhysicsSpace
                                          .TestCircle(position: fromPos,
                                                      radius: this.GetCurrentWeaponRange(),
                                                      collisionGroup: CollisionGroups.HitboxMelee);

            var objectOfInterest = objectsNearby.AsList()
                                   ?.Where(t => this.EnabledEntityList.Contains(t.PhysicsBody?.AssociatedWorldObject?.ProtoGameObject))
                                   .ToList();
            if (objectOfInterest == null || objectOfInterest.Count == 0)
            {
                return;
            }

            foreach (var obj in objectOfInterest)
            {
                var testWorldObject = obj.PhysicsBody.AssociatedWorldObject as IStaticWorldObject;
                var shape = obj.PhysicsBody.Shapes.FirstOrDefault(s =>
                                                                      s.CollisionGroup == CollisionGroups.HitboxMelee);
                if (shape == null)
                {
                    Api.Logger.Error("Automaton: target object has no HitBoxMelee shape " + testWorldObject);
                    continue;
                }
                if(!this.AdditionalValidation(testWorldObject))
                {
                    continue;
                }
                var targetPoint = this.ShapeCenter(shape) + obj.PhysicsBody.Position;
                if (this.CheckForObstacles(testWorldObject, targetPoint))
                {
                    this.AttackTarget(testWorldObject, targetPoint);
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
            Vector2D diff = targetObject.PhysicsBody.Position - weaponPos;

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

        private bool CheckForObstacles(IWorldObject targetObject, Vector2D intersectionPoint)
        {
            // Check for obstacles in line between character and object
            var fromPos = CurrentCharacter.Position + GetWeaponOffset();
            // Normalize vector and set it length to weapon range
            var toPos = (fromPos - intersectionPoint).Normalized * GetCurrentWeaponRange();
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
        }
    }
}
