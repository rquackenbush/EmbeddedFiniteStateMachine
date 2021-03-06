﻿using Autofac;
using Cas.Common.WPF;
using Cas.Common.WPF.Behaviors;
using Cas.Common.WPF.Interfaces;
using EFSM.Designer.Common;
using EFSM.Designer.Extensions;
using EFSM.Designer.Interfaces;
using EFSM.Designer.Messages;
using EFSM.Domain;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace EFSM.Designer.ViewModel
{
    public class StateMachineDialogWindowViewModel : ViewModelBase, IUndoProvider, ICloseableViewModel
    {
        private readonly IUndoService<StateMachine> _undoService;
        private readonly IViewService _viewService;
        private readonly IDirtyService _parentDirtyService;
        private IMessageBoxService _messageBoxService;

        public ICommand DeleteCommand { get; private set; }
        public ICommand OkCommand { get; private set; }
        public ICommand UndoCommand { get; private set; }
        public ICommand RedoCommand { get; private set; }
        public ICommand SimulationCommand { get; private set; }
        public ICommand CreateDocumentationCommand { get; private set; }
        public ICommand SelectAllCommand { get; private set; }
        public ICommand SaveCommand { get; private set; }

        private StateMachineViewModel _stateMachineViewModel;

        private OrderedListDesigner<StateMachineInputViewModel> _inputs;
        private OrderedListDesigner<StateMachineOutputActionViewModel> _outputs;

        private Action<StateMachine> _updateParentModel;

        public event EventHandler<CloseEventArgs> Close;

        public StateMachineDialogWindowViewModel(
            StateMachine stateMachine,
            IViewService viewService,
            IDirtyService parentDirtyService,
            Action<StateMachine> updateParentModel,
            IMessageBoxService messageBoxService
            )
        {
            _viewService = viewService ?? throw new ArgumentNullException(nameof(viewService));
            _parentDirtyService = parentDirtyService ?? throw new ArgumentNullException(nameof(parentDirtyService));
            _updateParentModel = updateParentModel;
            _messageBoxService = messageBoxService ?? throw new ArgumentNullException(nameof(messageBoxService));

            InitiateStateMachineViewModel(stateMachine);

            _undoService = new UndoService<StateMachine>();
            _undoService.Clear(SaveMomento());

            DirtyService.MarkClean();
            InitializeCommands();
        }

        public string Title => $"State Machine - {StateMachine.Name}";

        public OrderedListDesigner<StateMachineOutputActionViewModel> Outputs
        {
            get { return _outputs; }
            private set { _outputs = value; RaisePropertyChanged(); }
        }

        public OrderedListDesigner<StateMachineInputViewModel> Inputs
        {
            get { return _inputs; }
            private set { _inputs = value; RaisePropertyChanged(); }
        }

        public StateMachineViewModel StateMachine
        {
            get { return _stateMachineViewModel; }
            private set
            {
                if (_stateMachineViewModel != value)
                {
                    _stateMachineViewModel = value;
                    RaisePropertyChanged();
                }
            }
        }

        public IDirtyService DirtyService { get; } = new DirtyService();

        private void InitializeCommands()
        {
            DeleteCommand = new RelayCommand(Delete);
            OkCommand = new RelayCommand(OkButtonClick);
            UndoCommand = new RelayCommand(Undo, CanUndo);
            RedoCommand = new RelayCommand(Redo, CanRedo);
            SimulationCommand = new RelayCommand(Simulate);
            SelectAllCommand = new RelayCommand(SelectAll);
            SaveCommand = new RelayCommand(SaveProject, CanSave);
        }

        private bool CanSave() => DirtyService.IsDirty;

        private void SaveProject()
        {
            try
            {
                Save();
                MessengerInstance.Send(new SaveMessage());
            }
            catch (Exception ex)
            {
                _messageBoxService.Show(ex);
            }
        }

        private void SelectAll()
        {
            try
            {
                StateMachine.SelectionService.SelectNone();

                foreach (var state in StateMachine.States)
                {
                    StateMachine.SelectionService.Select(state);
                }
            }
            catch (Exception ex)
            {
                _messageBoxService.Show(ex);
            }
        }

        private void Simulate()
        {
            try
            {
                var viewModel = ApplicationContainer.Container.Resolve<SimulationViewModel>(
                new TypedParameter(typeof(StateMachine), GetModel())
                );

                _viewService.ShowDialog(viewModel);
            }
            catch (Exception ex)
            {
                _messageBoxService.Show(ex);
            }
        }

        private void InitiateStateMachineViewModel(StateMachine stateMachineModel)
        {
            StateMachine = ApplicationContainer.Container.Resolve<StateMachineViewModel>
               (
                new TypedParameter(typeof(StateMachine), stateMachineModel),
                new TypedParameter(typeof(IUndoProvider), this),
                new TypedParameter(typeof(IDirtyService), DirtyService)
               );

            Inputs = new OrderedListDesigner<StateMachineInputViewModel>(CreateInput, StateMachine.Inputs, addedAction: ConnectorAdded, deletedAction: InputDeleted);
            _inputs.ListChanged += ChildListChanged;

            Outputs = new OrderedListDesigner<StateMachineOutputActionViewModel>(CreateOutput, StateMachine.Outputs, addedAction: OutputAdded, deletedAction: OutputDeleted);
            _outputs.ListChanged += ChildListChanged;
        }

        public StateMachine GetModel() => StateMachine.GetModel();

        private StateMachineInputViewModel CreateInput()
        {
            DirtyService.MarkDirty();
            return new StateMachineInputViewModel(new StateMachineInput { Id = Guid.NewGuid(), Name = "Input" }, StateMachine);
        }

        private StateMachineOutputActionViewModel CreateOutput()
        {
            DirtyService.MarkDirty();
            return new StateMachineOutputActionViewModel(new StateMachineOutputAction { Id = Guid.NewGuid(), Name = "Output" }, StateMachine);
        }

        private void ChildListChanged(object sender, EventArgs e)
        {
            DirtyService.MarkDirty();
            SaveUndoState();
        }

        private void InputDeleted(StateMachineInputViewModel input)
        {
            foreach (var item in StateMachine.Transitions)
            {
                item.DeleteInput(input);
            }

            StateMachine.Inputs.Remove(input);
        }

        private void OutputDeleted(StateMachineOutputActionViewModel output)
        {
            var actionForDelete = StateMachine.Outputs.FirstOrDefault(o => o.Id == output.Id);

            if (actionForDelete != null)
            {
                StateMachine.Outputs.Remove(actionForDelete);
            }

            foreach (var item in StateMachine.Transitions)
            {
                item.DeleteOutputId(output.Id);
            }

            foreach (var item in StateMachine.States)
            {
                item.DeleteAction(output.Id);
            }
        }

        private void ConnectorAdded(StateMachineInputViewModel connector)
        {
            StateMachine.Inputs.Add(connector);
            DirtyService.MarkDirty();
        }

        private void OutputAdded(StateMachineOutputActionViewModel output)
        {
            StateMachine.Outputs.Add(output);
            DirtyService.MarkDirty();
        }


        private void OkButtonClick()
        {
            try
            {
                Save();
                Close?.Invoke(this, new CloseEventArgs(true));
            }
            catch (Exception ex)
            {
                _messageBoxService.Show(ex);
            }
        }

        private void Delete()
        {
            try
            {
                StateMachine.DeleteOfSelected();
            }
            catch (Exception ex)
            {
                _messageBoxService.Show(ex);
            }
        }

        private StateMachine SaveMomento() => GetModel();

        private void Redo()
        {
            try
            {
                InitiateStateMachineViewModel(_undoService.Redo());
            }
            catch (Exception ex)
            {
                _messageBoxService.Show(ex);
            }
        }

        private void Undo()
        {
            try
            {
                InitiateStateMachineViewModel(_undoService.Undo());
            }
            catch (Exception ex)
            {
                _messageBoxService.Show(ex);
            }
        }

        public void SaveUndoState()
        {
            _undoService.Do(SaveMomento());
        }

        public bool CanUndo() => _undoService.CanUndo();

        public bool CanRedo() => _undoService.CanRedo();

        public bool CanClose()
        {
            if (DirtyService.IsDirty)
            {
                var result = MessageBox.Show("Save changes?", "State Machine has changed", MessageBoxButton.YesNoCancel);

                switch (result)
                {
                    case MessageBoxResult.Yes:
                        Save();
                        break;
                    case MessageBoxResult.No:
                        return true;
                    case MessageBoxResult.Cancel:
                        return false;
                }
            }

            return true;
        }

        private void Save()
        {
            if (DirtyService.IsDirty)
            {
                DirtyService.MarkClean();
                _parentDirtyService.MarkDirty();
                _updateParentModel(GetModel());
            }
        }

        public void Closed()
        {
        }
    }
}