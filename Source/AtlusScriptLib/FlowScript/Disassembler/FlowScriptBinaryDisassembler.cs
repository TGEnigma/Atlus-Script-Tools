﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;

using AtlusScriptLib.Shared.Utilities;

namespace AtlusScriptLib.FlowScript.Disassembler
{
    public class FlowScriptBinaryDisassembler
    {
        private string mHeaderString = "This file was generated by AtlusScriptLib";
        private FlowScriptBinary mScript;
        private IDisassemblerTextOutput mOutput;     
        private int mInstructionIndex;

        public string HeaderString
        {
            get { return mHeaderString; }
            set { mHeaderString = value; }
        }

        private FlowScriptBinaryInstruction CurrentInstruction
        {
            get
            {
                if (mScript == null || mScript.TextSectionData == null || mScript.TextSectionData.Count == 0)
                    throw new InvalidDataException("Invalid state");

                return mScript.TextSectionData[mInstructionIndex];
            }
        }

        private FlowScriptBinaryInstruction? NextInstruction
        {
            get
            {
                if (mScript == null || mScript.TextSectionData == null || mScript.TextSectionData.Count == 0)
                    return null;

                if ((mInstructionIndex + 1) < (mScript.TextSectionData.Count - 1))
                    return mScript.TextSectionData[mInstructionIndex + 1];
                else
                    return null;
            }
        }

        public FlowScriptBinaryDisassembler(StringBuilder stringBuilder)
        {
            mOutput = new StringBuilderDisassemblerTextOutput(stringBuilder);
        }

        public FlowScriptBinaryDisassembler(TextWriter writer)
        {
            mOutput = new TextWriterDisassemblerTextOutput(writer);
        }

        public FlowScriptBinaryDisassembler(string outpath)
        {
            mOutput = new TextWriterDisassemblerTextOutput(new StreamWriter(outpath));
        }

        public FlowScriptBinaryDisassembler(Stream stream)
        {
            mOutput = new TextWriterDisassemblerTextOutput(new StreamWriter(stream));
        }

        public void Disassemble(FlowScriptBinary script)
        {
            mScript = script ?? throw new ArgumentNullException(nameof(script));
            mInstructionIndex = 0;

            PutDisassembly();
        }

        private void PutDisassembly()
        {
            PutHeader();
            PutTextDisassembly();
            PutMessageScriptDisassembly();
            mOutput.Dispose();
        }

        private void PutHeader()
        {
            mOutput.PutCommentLine(mHeaderString);
            mOutput.PutNewline();
        }

        private void PutTextDisassembly()
        {
            mOutput.PutLine(".text");

            while (mInstructionIndex < mScript.TextSectionData.Count)
            {
                // Check if there is a possible jump label at the current index
                var jumps = mScript.JumpLabelSectionData.Where(x => x.Offset == mInstructionIndex);

                foreach (var jump in jumps)
                {
                    mOutput.PutLine($"{jump.Name}:");
                }

                PutInstructionDisassembly();

                if (OpcodeUsesExtendedOperand(CurrentInstruction.Opcode))
                {
                    mInstructionIndex += 2;
                }
                else
                {
                    mInstructionIndex++;
                }
            }

            mOutput.PutNewline();
        }

        private bool OpcodeUsesExtendedOperand(FlowScriptBinaryOpcode opcode)
        {
            if (opcode == FlowScriptBinaryOpcode.PUSHI || opcode == FlowScriptBinaryOpcode.PUSHF)
                return true;

            return false;
        }

        private void PutInstructionDisassembly()
        {
            switch (CurrentInstruction.Opcode)
            {
                // extended int operand
                case FlowScriptBinaryOpcode.PUSHI:
                    mOutput.PutLine(DisassembleInstructionWithIntOperand(CurrentInstruction, NextInstruction.Value));
                    break;

                // extended float operand
                case FlowScriptBinaryOpcode.PUSHF:
                    mOutput.PutLine(DisassembleInstructionWithFloatOperand(CurrentInstruction, NextInstruction.Value));
                    break;

                // short operand
                case FlowScriptBinaryOpcode.PUSHIX:
                case FlowScriptBinaryOpcode.PUSHIF:
                case FlowScriptBinaryOpcode.POPIX:
                case FlowScriptBinaryOpcode.POPFX:
                case FlowScriptBinaryOpcode.RUN:
                case FlowScriptBinaryOpcode.PUSHIS:
                case FlowScriptBinaryOpcode.PUSHLIX:
                case FlowScriptBinaryOpcode.PUSHLFX:
                case FlowScriptBinaryOpcode.POPLIX:
                case FlowScriptBinaryOpcode.POPLFX:
                    mOutput.PutLine(DisassembleInstructionWithShortOperand(CurrentInstruction));
                    break;

                // string opcodes
                case FlowScriptBinaryOpcode.PUSHSTR:
                    mOutput.PutLine(DisassembleInstructionWithStringReferenceOperand(CurrentInstruction, mScript.StringSectionData));
                    break;

                // branch procedure opcodes
                case FlowScriptBinaryOpcode.PROC:
                case FlowScriptBinaryOpcode.CALL:
                    mOutput.PutLine(DisassembleInstructionWithLabelReferenceOperand(CurrentInstruction, mScript.ProcedureLabelSectionData));
                    break;

                // branch jump opcodes
                case FlowScriptBinaryOpcode.JUMP:           
                case FlowScriptBinaryOpcode.GOTO:
                case FlowScriptBinaryOpcode.IF:
                    mOutput.PutLine(DisassembleInstructionWithLabelReferenceOperand(CurrentInstruction, mScript.JumpLabelSectionData));
                    break;

                // branch communicate opcode
                case FlowScriptBinaryOpcode.COMM:
                    mOutput.PutLine(DisassembleInstructionWithCommReferenceOperand(CurrentInstruction));
                    break;

                // No operands
                case FlowScriptBinaryOpcode.PUSHREG:          
                case FlowScriptBinaryOpcode.ADD:
                case FlowScriptBinaryOpcode.SUB:               
                case FlowScriptBinaryOpcode.MUL:
                case FlowScriptBinaryOpcode.DIV:
                case FlowScriptBinaryOpcode.MINUS:
                case FlowScriptBinaryOpcode.NOT:
                case FlowScriptBinaryOpcode.OR:
                case FlowScriptBinaryOpcode.AND:
                case FlowScriptBinaryOpcode.EQ:
                case FlowScriptBinaryOpcode.NEQ:
                case FlowScriptBinaryOpcode.S:
                case FlowScriptBinaryOpcode.L:
                case FlowScriptBinaryOpcode.SE:
                case FlowScriptBinaryOpcode.LE:
                    mOutput.PutLine(DisassembleInstructionWithNoOperand(CurrentInstruction));
                    break;

                case FlowScriptBinaryOpcode.END:
                    mOutput.PutLine(DisassembleInstructionWithNoOperand(CurrentInstruction));
                    if (NextInstruction.HasValue)
                    {
                        if (NextInstruction.Value.Opcode != FlowScriptBinaryOpcode.END)
                            mOutput.PutNewline();
                    }
                    break;

                default:
                    DebugUtils.FatalException($"Unknown opcode {CurrentInstruction.Opcode}");
                    break;
            }
        }

        private void PutMessageScriptDisassembly()
        {
            mOutput.PutLine(".msgdata raw");
            for (int i = 0; i < mScript.MessageScriptSectionData.Count; i++)
            {
                mOutput.Put(mScript.MessageScriptSectionData[i].ToString("X2"));
            }
        }

        public static string DisassembleInstructionWithNoOperand(FlowScriptBinaryInstruction instruction)
        {
            if (instruction.OperandShort != 0)
            {
                DebugUtils.TraceError($"{instruction.Opcode} should not have any operands");
            }

            return $"{instruction.Opcode}";
        }

        public static string DisassembleInstructionWithIntOperand(FlowScriptBinaryInstruction instruction, FlowScriptBinaryInstruction operand)
        {
            return $"{instruction.Opcode} {operand.OperandInt}";
        }

        public static string DisassembleInstructionWithFloatOperand(FlowScriptBinaryInstruction instruction, FlowScriptBinaryInstruction operand)
        {
            return $"{instruction.Opcode} {operand.OperandFloat.ToString("0.00#####", CultureInfo.InvariantCulture)}f";
        }

        public static string DisassembleInstructionWithShortOperand(FlowScriptBinaryInstruction instruction)
        {
            return $"{instruction.Opcode} {instruction.OperandShort}";
        }

        public static string DisassembleInstructionWithStringReferenceOperand(FlowScriptBinaryInstruction instruction, IDictionary<int, string> stringMap)
        {
            if (!stringMap.ContainsKey(instruction.OperandShort))
            {
                DebugUtils.FatalException($"No string for string reference id {instruction.OperandShort} present in {nameof(stringMap)}");
            }

            return $"{instruction.Opcode} \"{stringMap[instruction.OperandShort]}\"";
        }

        public static string DisassembleInstructionWithLabelReferenceOperand(FlowScriptBinaryInstruction instruction, IList<FlowScriptBinaryLabel> labels)
        {
            if (instruction.OperandShort >= labels.Count)
            {
                DebugUtils.FatalException($"No label for label reference id {instruction.OperandShort} present in {nameof(labels)}");
            }

            return $"{instruction.Opcode} {labels[instruction.OperandShort].Name}";
        }

        public static string DisassembleInstructionWithCommReferenceOperand(FlowScriptBinaryInstruction instruction)
        {
            return $"{instruction.Opcode} {instruction.OperandShort}";
        }
    }
}
