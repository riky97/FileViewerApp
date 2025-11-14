using System;
using System.ComponentModel;
using System.Linq;
using System.Collections.ObjectModel;
using System.Collections.Generic; // aggiunto
using FileViewerApp.Services; // aggiunto per OpCodeService
using ReactiveUI;
using FileViewerApp.Enums;

namespace FileViewerApp.Models
{
    public class InstructionParameter : ReactiveObject
    {
        public int Index { get; }
        private int _value;
        private string _name = string.Empty; // Nome parametro da definizione (INFO.XML)
        public string Name 
        { 
            get => _name; 
            set 
            { 
                // Parameter name comes from metadata; changing it should NOT mark instruction modified.
                this.RaiseAndSetIfChanged(ref _name, value); 
            } 
        }
        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Index.ToString() : Name;
        public int Value
        {
            get => _value;
            set
            {
                if (_value == value) return;
                // Nessun clamping: permetti stato "fuori range" per feedback utente e blocco salvataggio
                this.RaiseAndSetIfChanged(ref _value, value);
                if (!HasResourceGroups && IsNumericType)
                {
                    ValidateRange(); // aggiorna IsRangeValid (false se fuori intervallo)
                }
            }
        }
        public InstructionParameter(int index, int value) { Index = index; _value = value; }

        // Metadata (filled from OpCodeInfo / INFO.XML)
        private bool _hasResourceGroups; // true se il PAR ha uno o più RESGROUP definiti
        public bool HasResourceGroups { get => _hasResourceGroups; set => this.RaiseAndSetIfChanged(ref _hasResourceGroups, value); }
        private double _minValue = int.MinValue;
        public double MinValue { get => _minValue; set => this.RaiseAndSetIfChanged(ref _minValue, value); }
        private double _maxValue = int.MaxValue;
        public double MaxValue { get => _maxValue; set => this.RaiseAndSetIfChanged(ref _maxValue, value); }
        private string _type = "INT"; // default
        public string Type { get => _type; set => this.RaiseAndSetIfChanged(ref _type, value); }
        private bool _isRangeValid = true;
        public bool IsRangeValid { get => _isRangeValid; private set => this.RaiseAndSetIfChanged(ref _isRangeValid, value); }
        public bool IsNumericType => string.Equals(Type, "INT", StringComparison.OrdinalIgnoreCase) || string.Equals(Type, "NUM", StringComparison.OrdinalIgnoreCase);

        public void SetMetadata(bool hasGroups, string? type, double min, double max, double? defaultValue)
        {
            HasResourceGroups = hasGroups;
            if (!string.IsNullOrWhiteSpace(type)) Type = type.Trim();
            MinValue = min;
            MaxValue = max;
            if (!hasGroups)
            {
                // Se c'è default e valore è 0 lo applico, ma NON clampo: voglio poter vedere valori fuori range provenienti dal file
                if (defaultValue.HasValue && _value == 0)
                {
                    _value = (int)Math.Round(defaultValue.Value);
                    this.RaisePropertyChanged(nameof(Value));
                }
            }
            ValidateRange();
        }

        public void SetName(string? name)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                Name = name.Trim();
                this.RaisePropertyChanged(nameof(DisplayName));
            }
        }

        private void ValidateRange()
        {
            if (HasResourceGroups) { IsRangeValid = true; return; }
            if (!IsNumericType) { IsRangeValid = true; return; }
            IsRangeValid = _value >= MinValue && _value <= MaxValue;
        }

        private ObservableCollection<ResourceOption>? _options;
        public ObservableCollection<ResourceOption>? Options => _options;
        public bool HasOptions => _options != null && _options.Count > 0;
        private ResourceOption? _selectedOption;
        public ResourceOption? SelectedOption
        {
            get => _selectedOption;
            set
            {
                if (_selectedOption == value) return;
                _selectedOption = value;
                this.RaisePropertyChanged();
                if (value?.Id.HasValue == true && Value != value.Id.Value)
                {
                    Value = value.Id.Value; // aggiorna Value -> triggers RaiseAndSetIfChanged
                    ValidateRange();
                }
            }
        }
        private string _filterText = string.Empty;
        public string FilterText { get => _filterText; set { this.RaiseAndSetIfChanged(ref _filterText, value); UpdateFilteredOptions(); } }
        private ObservableCollection<ResourceOption>? _filteredOptions;
        public ObservableCollection<ResourceOption>? FilteredOptions => _filteredOptions;
        public bool HasFilteredOptions => _filteredOptions != null && _filteredOptions.Count > 0;
        public bool IsValueValid => (_options != null && _options.Any(o => o.Id == Value));
        private ResourceOption _placeholder = new ResourceOption(new ResourceItem("PLACEHOLDER", null, "Seleziona...", "", null, null, null));

        public void SetOptions(IEnumerable<ResourceOption> opts, int currentValue)
        {
            if (_options == null) _options = new ObservableCollection<ResourceOption>();
            _options.Clear();
            foreach (var o in opts) _options.Add(o);
            this.RaisePropertyChanged(nameof(Options));
            this.RaisePropertyChanged(nameof(HasOptions));
            _selectedOption = _options.FirstOrDefault(o => o.Id == currentValue);
            this.RaisePropertyChanged(nameof(SelectedOption));
            UpdateFilteredOptions();
        }
        private void UpdateFilteredOptions()
        {
            if (_filteredOptions == null) _filteredOptions = new ObservableCollection<ResourceOption>();
            _filteredOptions.Clear();
            IEnumerable<ResourceOption> source = _options ?? Enumerable.Empty<ResourceOption>();
            if (!string.IsNullOrWhiteSpace(_filterText))
            {
                var ft = _filterText.Trim();
                bool numeric = int.TryParse(ft, out var num);
                source = source.Where(o => (numeric && o.Id == num) || o.Display.Contains(ft, StringComparison.OrdinalIgnoreCase));
            }
            foreach (var o in source) _filteredOptions.Add(o);
            // placeholder se nessuna o valore non valido
            if (_filteredOptions.Count == 0 || !IsValueValid)
            {
                if (!_filteredOptions.Contains(_placeholder))
                    _filteredOptions.Insert(0, _placeholder);
            }
            this.RaisePropertyChanged(nameof(FilteredOptions));
            this.RaisePropertyChanged(nameof(HasFilteredOptions));
            this.RaisePropertyChanged(nameof(IsValueValid));
        }

        private bool _instructionIsCurrent;
        public bool InstructionIsCurrent { get => _instructionIsCurrent; set => this.RaiseAndSetIfChanged(ref _instructionIsCurrent, value); }
        private bool _isSelected; // nuovo flag selezione globale parametro
        public bool IsSelected { get => _isSelected; set => this.RaiseAndSetIfChanged(ref _isSelected, value); }
    }

    public class EditableInstruction : ReactiveObject, INotifyPropertyChanged
    {
        private int _number;
        private int _opCode;
        private string _name = "";
        private int[] _parameters = new int[8];
        private bool _isValid = true;
        private bool _isModified = false;
        private string _validationSummary = ""; // was "OK" -> now empty when valid
        private string _instructionText = "";
        private int _paramCount = 8; // default until set by viewmodel

        // Evento per notificare quando una proprietà cambia
        public event EventHandler<PropertyChangedEventArgs>? InstructionChanged;

        public ObservableCollection<InstructionParameter> ParameterEntries { get; } = new();
        public ObservableCollection<InstructionParameter> VisibleParameterEntries { get; } = new();

        private bool _suppressChanges; // evita IsModified durante caricamenti snapshot
        private int _initializingSkipRemaining; // ignora i primi eventi parametri (sync UI)
        private bool _trackingEnabled = true; // nuovo flag per controllo esplicito

        public static OpCodeService? OpCodeServiceProvider { get; set; }

        private bool _isCurrent; // nuova proprietà per UI selezione
        private bool _isCutPending; // nuovo stato per taglio (visual blur finché non incollato)
        private InstructionDiffKind _diffKind = InstructionDiffKind.Unchanged;

        public EditableInstruction()
        {
            // Initialize parameter entries
            for (int i = 0; i < 8; i++)
            {
                var entry = new InstructionParameter(i, 0);
                entry.PropertyChanged += OnParameterEntryChanged;
                ParameterEntries.Add(entry);
            }
            UpdateVisibleParameters();
            LoadResourceOptions();
        }

        private void OnParameterEntryChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is InstructionParameter ip && e.PropertyName == nameof(InstructionParameter.Value))
            {
                _parameters[ip.Index] = ip.Value;
                // Se siamo in fase di caricamento o soppressione, ignora modifica
                if (_suppressChanges || _initializingSkipRemaining > 0)
                {
                    if (_initializingSkipRemaining > 0) _initializingSkipRemaining--;
                    return; // niente MarkAsModified
                }
                UpdateInstructionText();
                MarkAsModified();
                ValidateInstruction();
            }
        }

        public int Number { get => _number; set => this.RaiseAndSetIfChanged(ref _number, value); }
        public string Name 
        { 
            get => _name; 
            set 
            { 
                if (_name == value) return; 
                this.RaiseAndSetIfChanged(ref _name, value); 
                if (!_suppressChanges && _trackingEnabled) 
                {
                    MarkAsModified();
                }
            } 
        }
        public bool IsValid { get => _isValid; set => this.RaiseAndSetIfChanged(ref _isValid, value); }
        public bool IsModified { get => _isModified; set => this.RaiseAndSetIfChanged(ref _isModified, value); }
        public string ValidationSummary { get => _validationSummary; set => this.RaiseAndSetIfChanged(ref _validationSummary, value); }
        public bool IsCurrent
        {
            get => _isCurrent;
            set
            {
                if (_isCurrent == value) return;
                this.RaiseAndSetIfChanged(ref _isCurrent, value);
                foreach (var p in ParameterEntries)
                    p.InstructionIsCurrent = _isCurrent;
            }
        }

        public bool IsCutPending
        {
            get => _isCutPending;
            set
            {
                if (this.RaiseAndSetIfChanged(ref _isCutPending, value) && value)
                {
                    DiffKind = InstructionDiffKind.CutPending;
                }
            }
        }

        public InstructionDiffKind DiffKind
        {
            get => _diffKind;
            set => this.RaiseAndSetIfChanged(ref _diffKind, value);
        }

        public int ParamCount
        {
            get => _paramCount;
            set
            {
                if (_paramCount != value)
                {
                    this.RaiseAndSetIfChanged(ref _paramCount, value);
                    UpdateVisibleParameters();
                    ValidateInstruction();
                }
            }
        }

        private void UpdateVisibleParameters()
        {
            VisibleParameterEntries.Clear();
            foreach (var p in ParameterEntries.Take(Math.Clamp(ParamCount, 0, 8)))
                VisibleParameterEntries.Add(p);
            this.RaisePropertyChanged(nameof(VisibleParameterEntries));
        }

        public int OpCode
        {
            get => _opCode;
            set
            {
                if (_opCode != value)
                {
                    _opCode = value;
                    this.RaisePropertyChanged();
                    LoadResourceOptions();
                    UpdateInstructionText();
                    MarkAsModified();
                    ValidateInstruction();
                }
            }
        }

        // QUESTA È LA PROPRIETÀ PRINCIPALE PER IL DATAGRID
        public string InstructionText
        {
            get => _instructionText;
            set
            {
                if (_instructionText != value)
                {
                    _instructionText = value;
                    this.RaisePropertyChanged();
                    ParseInstructionText(value);
                    MarkAsModified();
                    ValidateInstruction();
                }
            }
        }

        // Legacy direct param properties retained for any existing bindings
        public int Param0 { get => _parameters[0]; set => SetParam(0, value); }
        public int Param1 { get => _parameters[1]; set => SetParam(1, value); }
        public int Param2 { get => _parameters[2]; set => SetParam(2, value); }
        public int Param3 { get => _parameters[3]; set => SetParam(3, value); }
        public int Param4 { get => _parameters[4]; set => SetParam(4, value); }
        public int Param5 { get => _parameters[5]; set => SetParam(5, value); }
        public int Param6 { get => _parameters[6]; set => SetParam(6, value); }
        public int Param7 { get => _parameters[7]; set => SetParam(7, value); }

        private void SetParam(int index, int value)
        {
            if (index < 0 || index >= _parameters.Length) return;
            if (_parameters[index] == value) return;
            _parameters[index] = value;
            ParameterEntries[index].Value = value; // sync entry
            this.RaisePropertyChanged($"Param{index}");
            UpdateInstructionText();
            MarkAsModified();
            ValidateInstruction();
        }

        // Metodi principali
        private void ParseInstructionText(string instructionText)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(instructionText)) return;

                var parts = instructionText.Trim().Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length == 0) return;

                // Prova a parsare il primo elemento come OpCode
                if (int.TryParse(parts[0], out int opCode))
                {
                    _opCode = opCode; // Assegna direttamente per evitare ricorsione
                    Name = GetOpCodeName(opCode);

                    // Parsa i parametri
                    Array.Clear(_parameters, 0, _parameters.Length);
                    for (int i = 1; i < parts.Length && i <= 8; i++)
                    {
                        if (int.TryParse(parts[i], out int param))
                        {
                            _parameters[i - 1] = param;
                        }
                    }
                }
                else
                {
                    // Formato testuale: "IF_NUM 1 2 3"
                    var foundOpCode = GetOpCodeFromName(parts[0].ToUpper());
                    if (foundOpCode != -1)
                    {
                        _opCode = foundOpCode;
                        Name = GetOpCodeName(foundOpCode);

                        // Parsa i parametri
                        Array.Clear(_parameters, 0, _parameters.Length);
                        for (int i = 1; i < parts.Length && i <= 8; i++)
                        {
                            if (int.TryParse(parts[i], out int param))
                            {
                                _parameters[i - 1] = param;
                            }
                        }
                    }
                }

                // Sync entries
                for (int i = 0; i < 8; i++) ParameterEntries[i].Value = _parameters[i];

                // Notifica le proprietà cambiate
                this.RaisePropertyChanged(nameof(OpCode));
                NotifyParametersChanged();
            }
            catch (Exception ex)
            {
                ValidationSummary = $"Errore parsing: {ex.Message}";
                IsValid = false;
            }
        }

        private void UpdateInstructionText()
        {
            try
            {
                var nonZeroParams = _parameters.Take(ParamCount).Where(p => p != 0).ToArray();
                _instructionText = nonZeroParams.Length > 0 ? $"{OpCode} {string.Join(" ", nonZeroParams)}" : OpCode.ToString();
                this.RaisePropertyChanged(nameof(InstructionText));
            }
            catch
            {
                _instructionText = OpCode.ToString();
                this.RaisePropertyChanged(nameof(InstructionText));
            }
        }

        private void NotifyParametersChanged()
        {
            for (int i = 0; i < 8; i++) this.RaisePropertyChanged($"Param{i}");
        }

        private void MarkAsModified()
        {
            if (_suppressChanges || !_trackingEnabled) return;
            IsModified = true;
            if (DiffKind == InstructionDiffKind.Unchanged)
                DiffKind = InstructionDiffKind.Modified;
            InstructionChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsModified)));
        }

        private void ValidateInstruction()
        {
            try
            {
                switch (OpCode)
                {
                    case 1: // NULLA
                        if (_parameters.Any(p => p != 0)) { ValidationSummary = "NULLA non dovrebbe avere parametri"; IsValid = false; }
                        else { ValidationSummary = ""; IsValid = true; }
                        break;
                    default:
                        ValidationSummary = ""; IsValid = true;
                        break;
                }
            }
            catch (Exception ex)
            {
                ValidationSummary = $"Errore validazione: {ex.Message}";
                IsValid = false;
            }
        }

        // Utility methods
        public void ResetModifications() { IsModified = false; ValidationSummary = ""; IsValid = true; }
        public int[] GetParameters() => (int[])_parameters.Clone();
        public void SetParameters(int[] parameters)
        {
            Array.Clear(_parameters, 0, _parameters.Length);
            Array.Copy(parameters, _parameters, Math.Min(parameters.Length, _parameters.Length));
            for (int i = 0; i < 8; i++)
            {
                // aggiornamento diretto senza trigger interno quando in silent
                if (_suppressChanges)
                {
                    ParameterEntries[i].PropertyChanged -= OnParameterEntryChanged;
                    ParameterEntries[i].Value = _parameters[i];
                    ParameterEntries[i].PropertyChanged += OnParameterEntryChanged;
                }
                else
                {
                    ParameterEntries[i].Value = _parameters[i];
                }
            }
            NotifyParametersChanged();
            UpdateInstructionText();
            if (!_suppressChanges)
                ValidateInstruction();
        }

        public void DisableModificationTracking()
        {
            _trackingEnabled = false;
            _suppressChanges = true; // garantisce nessun IsModified
        }
        public void EnableModificationTracking()
        {
            _trackingEnabled = true;
            _suppressChanges = false;
            _initializingSkipRemaining = 0;
        }

        public void BeginSilentUpdate()
        {
            _suppressChanges = true;
            _trackingEnabled = false; // disabilita fino a enable esplicito
            _initializingSkipRemaining = 0;
        }
        public void EndSilentUpdate()
        {
            // Mantieni soppressione; tracking resta disabilitato finché abilitato esplicitamente dal ViewModel
            IsModified = false;
            _initializingSkipRemaining = ParamCount; // ignora eventuali rimbalzi
        }

        public void LoadResourceOptions()
        {
            var svc = OpCodeServiceProvider;
            if (svc == null) return;
            System.Diagnostics.Debug.WriteLine($"[RES] LoadResourceOptions for Instruction #{Number} Name='{Name}' OpCode={OpCode}");
            for (int i = 0; i < ParameterEntries.Count; i++)
            {
                var opts = svc.GetParamOptions(OpCode, i);
                if (!opts.Any() && !string.IsNullOrWhiteSpace(Name))
                {
                    opts = svc.GetParamOptionsByName(Name, i);
                }
                System.Diagnostics.Debug.WriteLine($"[RES] Param {i} groups count={opts.Count()} value={_parameters[i]}");
                ParameterEntries[i].SetOptions(opts, _parameters[i]);

                // Metadata da OpCodeInfo se disponibile
                var info = svc.GetOpCodeInfo(OpCode);
                ParameterInfo? pinfo = null;
                if (info != null && i < info.Parameters.Count)
                    pinfo = info.Parameters[i];
                var hasGroups = opts.Any();
                var type = pinfo?.Type ?? null;
                double min = pinfo?.MinValue ?? int.MinValue;
                double max = pinfo?.MaxValue ?? int.MaxValue;
                double? def = pinfo?.DefaultValue;
                ParameterEntries[i].SetMetadata(hasGroups, type, min, max, def);
                ParameterEntries[i].SetName(pinfo?.Name);
            }
        }

        private string GetOpCodeName(int opCode) => opCode switch
        {
            1 => "NULLA",
            5 => "INIZ MEM",
            15 => "END IF",
            105 => "IF_NUM",
            110 => "IF_STR",
            200 => "GOTO",
            210 => "CALL",
            255 => "END",
            _ => $"UNKNOWN_{opCode}"
        };

        private int GetOpCodeFromName(string name) => name switch
        {
            "NULLA" => 1,
            "INIZ_MEM" or "INIZ MEM" => 5,
            "END_IF" or "END IF" => 15,
            "IF_NUM" or "IF NUM" => 105,
            "IF_STR" or "IF STR" => 110,
            "GOTO" => 200,
            "CALL" => 210,
            "END" => 255,
            _ => -1
        };
    }
}