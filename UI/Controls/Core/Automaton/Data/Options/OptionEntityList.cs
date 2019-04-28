﻿namespace CryoFall.Automaton.UI.Controls.Core.Data.Options
{
    using AtomicTorch.CBND.CoreMod.UI.Controls.Core;
    using AtomicTorch.CBND.GameApi.Data;
    using AtomicTorch.CBND.GameApi.Scripting;
    using AtomicTorch.CBND.GameApi.ServicesClient;
    using AtomicTorch.GameEngine.Common.Client.MonoGame.UI;
    using AtomicTorch.GameEngine.Common.Extensions;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;

    public class OptionEntityList : BaseViewModel, IOption
    {
        private bool isModified = false;

        public bool IsCosmetic => false;

        public bool IsModified
        {
            get { return isModified;}
            private set
            {
                if (value == isModified)
                {
                    return;
                }

                isModified = value;
                NotifyThisPropertyChanged();
                OnIsModifiedChanged?.Invoke();
            }
        }

        public event Action OnIsModifiedChanged;

        public ObservableCollection<ViewModelEntity> EntityCollection { get; set; }

        public BaseCommand SelectAll => new ActionCommand(() =>
        {
            EntityCollection.ForEach(v => v.IsEnabled = true);
            IsModified = true;
            NotifyPropertyChanged(nameof(HasUnselected));
        });

        public bool HasUnselected => EntityCollection.Any(e => !e.IsEnabled);

        public BaseCommand DeselectAll => new ActionCommand(() =>
        {
            EntityCollection.ForEach(v => v.IsEnabled = false);
            IsModified = true;
            NotifyPropertyChanged(nameof(HasSelected));
        });

        public bool HasSelected => EntityCollection.Any(e => e.IsEnabled);

        public string Id { get; }

        public List<IProtoEntity> SavedEnabledList = new List<IProtoEntity>();

        private readonly List<string> DefaultEnabledList;

        private event Action<List<IProtoEntity>> OnEnabledListChanged;

        public OptionEntityList(string id, IEnumerable<ViewModelEntity> entityList, List<string> defaultEnabledList,
            Action<List<IProtoEntity>> onEnabledListChanged)
        {
            Id = id;
            EntityCollection = new ObservableCollection<ViewModelEntity>(entityList);
            DefaultEnabledList = defaultEnabledList;
            foreach (var viewModelEntity in EntityCollection)
            {
                viewModelEntity.IsEnabledChanged += () => RefreshIsModified();
                if (defaultEnabledList?.Count > 0 && defaultEnabledList.Contains(viewModelEntity.Id))
                {
                    SavedEnabledList.Add(viewModelEntity.Entity);
                }
            }
            OnEnabledListChanged = onEnabledListChanged;
        }

        public void Apply()
        {
            SavedEnabledList.Clear();
            SavedEnabledList = GetEnabledEntityList();
            OnEnabledListChanged?.Invoke(SavedEnabledList);
            RefreshIsModified();
        }

        public void Cancel()
        {
            foreach (var viewModelEntity in EntityCollection)
            {
                if (SavedEnabledList?.Count > 0 && SavedEnabledList.Contains(viewModelEntity.Entity))
                {
                    viewModelEntity.IsEnabled = true;
                }
                else
                {
                    viewModelEntity.IsEnabled = false;
                }
            }
            RefreshIsModified();
        }

        private List<IProtoEntity> GetEnabledEntityList()
        {
            return EntityCollection.Where(e => e.IsEnabled).Select(e => e.Entity).ToList();
        }

        public void Reset(bool apply)
        {
            ApplyAbstractValue(DefaultEnabledList);
        }

        public void ApplyAbstractValue(object value)
        {
            if (value is List<string> enabledList)
            {
                foreach (var viewModelEntity in EntityCollection)
                {
                    if (enabledList?.Count > 0 && enabledList.Contains(viewModelEntity.Id))
                    {
                        viewModelEntity.IsEnabled = true;
                    }
                    else
                    {
                        viewModelEntity.IsEnabled = false;
                    }
                }
                Apply();
                return;
            }

            Api.Logger.Warning(
                $"Option {Id} cannot apply abstract value - type mismatch. Will reset option to the default value");
            Reset(apply: true);
        }

        public object GetAbstractValue()
        {
            return SavedEnabledList.Select(e => e.Id).ToList();
        }

        public void RegisterValueType(IClientStorage storage)
        {
            // List<string> should be already registred.
            // storage.RegisterType(typeof(List<string>));
        }

        private void RefreshIsModified()
        {
            if (SavedEnabledList.SequenceEqual(GetEnabledEntityList()))
            {
                IsModified = false;
            }
            else
            {
                IsModified = true;
            }
            NotifyPropertyChanged(nameof(HasSelected));
            NotifyPropertyChanged(nameof(HasUnselected));
        }
    }
}