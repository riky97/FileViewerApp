using System;
using System.ComponentModel;
using System.Linq;
using ReactiveUI;

namespace FileViewerApp.Models
{
    public class EditableInstruction : ReactiveObject, INotifyPropertyChanged
    {
        private int _number;
        private int _opCode;
        private string _name = "";
        private int[] _parameters = new int[8]; // Array semplice invece di 8 proprietà
        private bool _isValid = true;
        private bool _isModified = false;
        private string _validationSummary = "OK";
        private string _instructionText = "";

        // Evento per notificare quando una proprietà cambia
        public event EventHandler<PropertyChangedEventArgs>? InstructionChanged;

        public int Number
        {
            get => _number;
            set => this.RaiseAndSetIfChanged(ref _number, value);
        }

        public string Name
        {
            get => _name;
            set => this.RaiseAndSetIfChanged(ref _name, value);
        }

        public bool IsValid
        {
            get => _isValid;
            set => this.RaiseAndSetIfChanged(ref _isValid, value);
        }

        public bool IsModified
        {
            get => _isModified;
            set => this.RaiseAndSetIfChanged(ref _isModified, value);
        }

        public string ValidationSummary
        {
            get => _validationSummary;
            set => this.RaiseAndSetIfChanged(ref _validationSummary, value);
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

                    // Auto-aggiorna il nome quando cambia l'OpCode
                    Name = GetOpCodeName(value);
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

                    // Parsa la stringa dell'istruzione e aggiorna OpCode e parametri
                    ParseInstructionText(value);
                    MarkAsModified();
                    ValidateInstruction();
                }
            }
        }

        // Proprietà di compatibilità (se hai ancora bisogno)
        public int Param0 => _parameters[0];
        public int Param1 => _parameters[1];
        public int Param2 => _parameters[2];
        public int Param3 => _parameters[3];
        public int Param4 => _parameters[4];
        public int Param5 => _parameters[5];
        public int Param6 => _parameters[6];
        public int Param7 => _parameters[7];

        // Metodi principali
        private void ParseInstructionText(string instructionText)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(instructionText))
                    return;

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

                // Notifica le proprietà cambiate
                this.RaisePropertyChanged(nameof(OpCode));
                this.RaisePropertyChanged(nameof(Name));
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
                var nonZeroParams = _parameters.Where(p => p != 0).ToArray();
                if (nonZeroParams.Length > 0)
                {
                    var paramString = string.Join(" ", nonZeroParams);
                    _instructionText = $"{OpCode} {paramString}";
                }
                else
                {
                    _instructionText = OpCode.ToString();
                }

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
            this.RaisePropertyChanged(nameof(Param0));
            this.RaisePropertyChanged(nameof(Param1));
            this.RaisePropertyChanged(nameof(Param2));
            this.RaisePropertyChanged(nameof(Param3));
            this.RaisePropertyChanged(nameof(Param4));
            this.RaisePropertyChanged(nameof(Param5));
            this.RaisePropertyChanged(nameof(Param6));
            this.RaisePropertyChanged(nameof(Param7));
        }

        private void MarkAsModified()
        {
            IsModified = true;
            InstructionChanged?.Invoke(this, new PropertyChangedEventArgs("Modified"));
        }

        private void ValidateInstruction()
        {
            try
            {
                switch (OpCode)
                {
                    case 1: // NULLA
                        if (_parameters.Any(p => p != 0))
                        {
                            ValidationSummary = "NULLA non dovrebbe avere parametri";
                            IsValid = false;
                        }
                        else
                        {
                            ValidationSummary = "OK";
                            IsValid = true;
                        }
                        break;

                    case 105: // IF_NUM
                        if (_parameters[0] == 0 && _parameters[1] == 0 && _parameters[2] == 0)
                        {
                            ValidationSummary = "IF_NUM richiede parametri validi";
                            IsValid = false;
                        }
                        else
                        {
                            ValidationSummary = "OK";
                            IsValid = true;
                        }
                        break;

                    case 200: // GOTO
                        if (_parameters[0] == 0 && _parameters[1] == 0)
                        {
                            ValidationSummary = "GOTO richiede un indirizzo";
                            IsValid = false;
                        }
                        else
                        {
                            ValidationSummary = "OK";
                            IsValid = true;
                        }
                        break;

                    default:
                        ValidationSummary = "OK";
                        IsValid = true;
                        break;
                }
            }
            catch (Exception ex)
            {
                ValidationSummary = $"Errore validazione: {ex.Message}";
                IsValid = false;
            }
        }

        private string GetOpCodeName(int opCode)
        {
            return opCode switch
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
        }

        private int GetOpCodeFromName(string name)
        {
            return name switch
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

        // Metodi di utility
        public void ResetModifications()
        {
            IsModified = false;
            ValidationSummary = "OK";
            IsValid = true;
        }

        public int[] GetParameters()
        {
            return (int[])_parameters.Clone();
        }

        public void SetParameters(int[] parameters)
        {
            Array.Clear(_parameters, 0, _parameters.Length);
            Array.Copy(parameters, _parameters, Math.Min(parameters.Length, _parameters.Length));

            NotifyParametersChanged();
            UpdateInstructionText();
            ValidateInstruction();
        }

    }
}