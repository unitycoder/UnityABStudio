namespace SoarCraft.QYun.UnityABStudio.Helpers.ShaderConverters.SpirV {
    using System;
    using System.Collections.Generic;
    using System.Text;

    public class ParsedOperand {
        public ParsedOperand(IReadOnlyList<uint> words, int index, int count, object value, Operand operand) {
            uint[] array = new uint[count];
            for (int i = 0; i < count; i++) {
                array[i] = words[index + i];
            }

            this.Words = array;
            this.Value = value;
            this.Operand = operand;
        }

        public T GetSingleEnumValue<T>()
            where T : Enum {
            IValueEnumOperandValue v = (IValueEnumOperandValue)this.Value;
            if (v.Value.Count == 0) {
                // If there's no value at all, the enum is probably something like ImageFormat.
                // In which case we just return the enum value
                return (T)v.Key;
            } else {
                // This means the enum has a value attached to it, so we return the attached value
                return (T)((IValueEnumOperandValue)this.Value).Value[0];
            }
        }

        public uint GetId() {
            return ((ObjectReference)this.Value).Id;
        }

        public T GetBitEnumValue<T>()
            where T : Enum {
            var v = this.Value as IBitEnumOperandValue;

            uint result = 0;
            foreach (var k in v.Values.Keys) {
                result |= k;
            }

            return (T)(object)result;
        }

        public IReadOnlyList<uint> Words { get; }
        public object Value { get; set; }
        public Operand Operand { get; }
    }

    public class VaryingOperandValue {
        public VaryingOperandValue(IReadOnlyList<object> values) {
            this.Values = values;
        }

        public override string ToString() {
            StringBuilder sb = new StringBuilder();
            this.ToString(sb);
            return sb.ToString();
        }

        public StringBuilder ToString(StringBuilder sb) {
            for (int i = 0; i < this.Values.Count; ++i) {
                if (this.Values[i] is ObjectReference objRef) {
                    objRef.ToString(sb);
                } else {
                    sb.Append(this.Values[i]);
                }
                if (i < (this.Values.Count - 1)) {
                    sb.Append(' ');
                }
            }
            return sb;
        }

        public IReadOnlyList<object> Values { get; }
    }

    public interface IEnumOperandValue {
        System.Type EnumerationType { get; }
    }

    public interface IBitEnumOperandValue : IEnumOperandValue {
        IReadOnlyDictionary<uint, IReadOnlyList<object>> Values { get; }
    }

    public interface IValueEnumOperandValue : IEnumOperandValue {
        object Key { get; }
        IReadOnlyList<object> Value { get; }
    }

    public class ValueEnumOperandValue<T> : IValueEnumOperandValue
        where T : Enum {
        public ValueEnumOperandValue(T key, IReadOnlyList<object> value) {
            this.Key = key;
            this.Value = value;
        }

        public System.Type EnumerationType => typeof(T);
        public object Key { get; }
        public IReadOnlyList<object> Value { get; }
    }

    public class BitEnumOperandValue<T> : IBitEnumOperandValue
        where T : Enum {
        public BitEnumOperandValue(Dictionary<uint, IReadOnlyList<object>> values) {
            this.Values = values;
        }

        public IReadOnlyDictionary<uint, IReadOnlyList<object>> Values { get; }
        public System.Type EnumerationType => typeof(T);
    }

    public class ObjectReference {
        public ObjectReference(uint id) {
            this.Id = id;
        }

        public void Resolve(IReadOnlyDictionary<uint, ParsedInstruction> objects) {
            this.Reference = objects[this.Id];
        }

        public override string ToString() {
            return $"%{this.Id}";
        }

        public StringBuilder ToString(StringBuilder sb) {
            return sb.Append('%').Append(this.Id);
        }

        public uint Id { get; }
        public ParsedInstruction Reference { get; private set; }
    }

    public class ParsedInstruction {
        public ParsedInstruction(int opCode, IReadOnlyList<uint> words) {
            this.Words = words;
            this.Instruction = Instructions.OpcodeToInstruction[opCode];
            this.ParseOperands();
        }

        private void ParseOperands() {
            if (this.Instruction.Operands.Count == 0) {
                return;
            }

            // Word 0 describes this instruction so we can ignore it
            int currentWord = 1;
            int currentOperand = 0;
            List<object> varyingOperandValues = new List<object>();
            int varyingWordStart = 0;
            Operand varyingOperand = null;

            while (currentWord < this.Words.Count) {
                Operand operand = this.Instruction.Operands[currentOperand];
                operand.Type.ReadValue(this.Words, currentWord, out object value, out int wordsUsed);
                if (operand.Quantifier == OperandQuantifier.Varying) {
                    varyingOperandValues.Add(value);
                    varyingWordStart = currentWord;
                    varyingOperand = operand;
                } else {
                    int wordCount = Math.Min(this.Words.Count - currentWord, wordsUsed);
                    ParsedOperand parsedOperand = new ParsedOperand(this.Words, currentWord, wordCount, value, operand);
                    this.Operands.Add(parsedOperand);
                }

                currentWord += wordsUsed;
                if (operand.Quantifier != OperandQuantifier.Varying) {
                    ++currentOperand;
                }
            }

            if (varyingOperand != null) {
                VaryingOperandValue varOperantValue = new VaryingOperandValue(varyingOperandValues);
                ParsedOperand parsedOperand = new ParsedOperand(this.Words, currentWord, this.Words.Count - currentWord, varOperantValue, varyingOperand);
                this.Operands.Add(parsedOperand);
            }
        }

        public void ResolveResultType(IReadOnlyDictionary<uint, ParsedInstruction> objects) {
            if (this.Instruction.Operands.Count > 0 && this.Instruction.Operands[0].Type is IdResultType) {
                this.ResultType = objects[(uint)this.Operands[0].Value].ResultType;
            }
        }

        public void ResolveReferences(IReadOnlyDictionary<uint, ParsedInstruction> objects) {
            foreach (var operand in this.Operands) {
                if (operand.Value is ObjectReference objectReference) {
                    objectReference.Resolve(objects);
                }
            }
        }

        public Type ResultType { get; set; }
        public uint ResultId {
            get {
                for (int i = 0; i < this.Instruction.Operands.Count; ++i) {
                    if (this.Instruction.Operands[i].Type is IdResult) {
                        return this.Operands[i].GetId();
                    }
                }
                return 0;
            }
        }
        public bool HasResult => this.ResultId != 0;

        public IReadOnlyList<uint> Words { get; }
        public Instruction Instruction { get; }
        public IList<ParsedOperand> Operands { get; } = new List<ParsedOperand>();
        public string Name { get; set; }
        public object Value { get; set; }
    }
}
