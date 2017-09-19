﻿using System;
using EFSM.Domain;

namespace EFSM.Designer.ViewModel.TransitionEditor
{
    public class SimpleConditionViewModel : ConditionViewModelBase
    {
        private Guid _sourceInputId;
        private bool _value;

        public SimpleConditionViewModel(TransitionEditorViewModel owner)
            : base(owner)
        {
        }

        public Guid SourceInputId
        {
            get { return _sourceInputId; }
            set
            {
                _sourceInputId = value;
                RaisePropertyChanged();
                DirtyService.MarkDirty();
            }
        }

        public bool Value
        {
            get { return _value; }
            set
            {
                _value = value;
                RaisePropertyChanged();
                DirtyService.MarkDirty();
            }
        }

        internal override StateMachineCondition GetModel()
        {
            return new StateMachineCondition()
            {
                SourceInputId = SourceInputId,
                Value = Value
            };
        }
    }
}