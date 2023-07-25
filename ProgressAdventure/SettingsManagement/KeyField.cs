﻿using ProgressAdventure.Enums;
using ProgressAdventure.Extensions;
using SaveFileManager;
using System.Text;

namespace ProgressAdventure.SettingsManagement
{
    /// <summary>
    /// Object for the <c>OptionsUI</c> method.<br/>
    /// When used as input in the <c>OptionsUI</c> function, it draws a field for one keypress, that can be selected to edit it's value in place, with the enter action.<br/>
    /// Structure: [<c>preText</c>][<c>value</c>][<c>postValue</c>]
    /// </summary>
    public class KeyField : BaseUI
    {
        #region Public fields
        /// <summary>
        /// The current value of the object.
        /// </summary>
#pragma warning disable CS0108 // Hiding was intended
        public ActionKey value;
#pragma warning restore CS0108 // Hiding was intended
        /// <summary>
        /// A function to return the status of the key, the user inputed.
        /// </summary>
        public ValidatorDelegate? validatorFunction;
        /// <summary>
        /// A function to return the display value of the value of the <c>ActionKey</c>.
        /// </summary>
        public DisplayValueDelegate? displayValueFunction;
        /// <summary>
        /// The number of keys to request for the <c>ActionKey</c>.
        /// </summary>
        public int keyNum;
        /// <summary>
        /// Wether to interpret string lengths as the length of the string as it will be displayed in the terminal, or just the string.Length.
        /// </summary>
        public bool lengthAsDisplayLength;
        #endregion

        #region Public delegates
        /// <summary>
        /// <inheritdoc cref="validatorFunction" path="//summary"/>
        /// </summary>
        /// <param name="key">The key that the user inputed.</param>
        /// <param name="keyField">The <c>KeyField</c> that the called this function.</param>
        public delegate (TextFieldValidatorStatus status, string? message) ValidatorDelegate(ConsoleKeyInfo key, KeyField keyField);
        /// <summary>
        /// <inheritdoc cref="displayValueFunction" path="//summary"/>
        /// </summary>
        /// <inheritdoc cref="BaseUI.MakeSpecial"/>
        /// <param name="keyField">The <c>KeyField</c>, that called the function.</param>
        public delegate string DisplayValueDelegate(KeyField keyField, string icons, OptionsUI? optionsUI = null);
        #endregion

        #region Constructors
        /// <summary>
        /// <inheritdoc cref="KeyField"/>
        /// </summary>
        /// <param name="value"><inheritdoc cref="value" path="//summary"/></param>
        /// <param name="actionType"><inheritdoc cref="actionType" path="//summary"/></param>
        /// <param name="validatorFunction"><inheritdoc cref="validatorFunction" path="//summary"/></param>
        /// <param name="keyNum"><inheritdoc cref="keyNum" path="//summary"/></param>
        /// <param name="lengthAsDisplayLength"><inheritdoc cref="lengthAsDisplayLength" path="//summary"/></param>
        /// <inheritdoc cref="BaseUI(int, string, string, bool, string, bool)"/>
        public KeyField(ActionKey value, string preText = "", string postValue = "", bool multiline = false, ValidatorDelegate? validatorFunction = null, DisplayValueDelegate? displayValueFunction = null, int keyNum = 1, bool lengthAsDisplayLength = true)
            : base(-1, preText, "", false, postValue, multiline)
        {
            this.value = value;
            this.validatorFunction = validatorFunction;
            this.displayValueFunction = displayValueFunction;
            this.keyNum = keyNum;
            this.lengthAsDisplayLength = lengthAsDisplayLength;
        }
        #endregion

        #region Override methods
        /// <inheritdoc cref="BaseUI.MakeSpecial"/>
        protected override string MakeSpecial(string icons, OptionsUI? optionsUI = null)
        {
            return displayValueFunction is not null ? displayValueFunction(this, icons, optionsUI) : string.Join(", ", value.Names);
        }

        /// <inheritdoc cref="BaseUI.HandleAction"/>
        public override object HandleAction(object key, IEnumerable<object> keyResults, IEnumerable<KeyAction>? keybinds = null, OptionsUI? optionsUI = null)
        {
            if (key.Equals(keyResults.ElementAt((int)Key.ENTER)))
            {
                if (optionsUI == null || !optionsUI.elements.Any(element => element == this))
                {
                    Console.WriteLine(preText);
                    var keys = new List<ConsoleKeyInfo>();
                    for (int x = 0; x < keyNum; x++)
                    {
                        var pressedKey = Console.ReadKey();
                        keys.Add(pressedKey);
                        Console.Write(SettingsUtils.GetKeyName(pressedKey));
                        if (x < keyNum - 1)
                        {
                            Console.Write(", ");
                        }
                    }
                    value.Keys = keys;
                }
                else
                {
                    var xOffset = GetCurrentLineCharCountBeforeValue(optionsUI.cursorIcon);
                    var yOffset = GetLineNumberAfterTextFieldValue(optionsUI);
                    Utils.MoveCursor((xOffset, yOffset));

                    var keys = new List<ConsoleKeyInfo>();

                    for (int x = 0; x < keyNum; x++)
                    {
                        bool retry;
                        do
                        {
                            retry = false;
                            var newValue = ReadInput(optionsUI.cursorIcon, keys);
                            if (validatorFunction is null)
                            {
                                keys.Add(newValue);
                                value.Keys = keys;
                            }
                            else
                            {
                                var keysBak = value.Keys.DeepCopy();
                                keys.Add(newValue);
                                value.Keys = keys;
                                var (status, message) = validatorFunction(newValue, this);
                                if (message != null)
                                {
                                    (int preMessageLeft, int preMessageTop) = Console.GetCursorPosition();
                                    Console.Write("\u001b[0K" + message);
                                    Console.ReadKey(true);
                                    Console.SetCursorPosition(preMessageLeft, preMessageTop);
                                    Console.Write("\u001b[0K");
                                    var (Left, Top) = Console.GetCursorPosition();
                                    if (multiline)
                                    {
                                        Console.Write(postValue.Replace("\n", optionsUI.cursorIcon.sIconR + "\n" + optionsUI.cursorIcon.sIcon));
                                    }
                                    else
                                    {
                                        Console.Write(postValue);
                                    }
                                    Console.SetCursorPosition(Left, Top);
                                }
                                if (status != TextFieldValidatorStatus.VALID)
                                {
                                    value.Keys = keysBak;
                                    keys = keysBak.ToList();
                                }
                                else if (status == TextFieldValidatorStatus.RETRY)
                                {
                                    retry = true;
                                }
                            }
                        }
                        while (retry);
                    }
                }

                return true;
            }
            return true;
        }

        /// <inheritdoc cref="BaseUI.IsOnlyClickable"/>
        public override bool IsOnlyClickable()
        {
            return true;
        }
        #endregion

        #region Private functions
        /// <summary>
        /// Gets the number of lines after the value that is in this object, in the display.
        /// </summary>
        /// <param name="optionsUI">The <c>OptionsUI</c>, that includes this object.</param>
        private int GetLineNumberAfterTextFieldValue(OptionsUI optionsUI)
        {
            var foundTextField = false;
            var txt = new StringBuilder();

            // current object's line
            if (multiline)
            {
                txt.Append(postValue.Replace("\n", optionsUI.cursorIcon.sIconR + "\n" + optionsUI.cursorIcon.sIcon));
            }
            else
            {
                txt.Append(postValue);
            }
            txt.Append(optionsUI.cursorIcon.sIconR);

            // lines after current object
            for (var x = 0; x < optionsUI.elements.Count(); x++)
            {
                var element = optionsUI.elements.ElementAt(x);
                if (foundTextField)
                {
                    if (element is not null && typeof(BaseUI).IsAssignableFrom(element.GetType()))
                    {
                        txt.Append(element.MakeText(
                            optionsUI.cursorIcon.icon,
                            optionsUI.cursorIcon.iconR,
                            optionsUI
                        ));
                    }
                    else if (element is null)
                    {
                        txt.Append('\n');
                    }
                    else
                    {
                        txt.Append(element.ToString() + "\n");
                    }
                }
                else
                {
                    if (element == this)
                    {
                        foundTextField = true;
                    }
                }
            }
            txt.Append('\n');

            return txt.ToString().Count(c => c == '\n') + 1;
        }

        /// <summary>
        /// Gets the number of characters in this object's display line string, before the value.
        /// </summary>
        /// <param name="cursorIcon">The <c>CursorIcon</c> passed into the <c>OptionsUI</c>, that includes this object.</param>
        private int GetCurrentLineCharCountBeforeValue(CursorIcon cursorIcon)
        {
            var lineText = new StringBuilder();
            lineText.Append(cursorIcon.sIcon);
            if (multiline)
            {
                lineText.Append(preText.Replace("\n", cursorIcon.sIconR + "\n" + cursorIcon.sIcon));
            }
            else
            {
                lineText.Append(preText);
            }
            var lastLine = lineText.ToString().Split("\n").Last();
            return lengthAsDisplayLength ? Utils.GetDisplayLen(lastLine) : lastLine.Length;
        }

        /// <summary>
        /// Reads user input, like <c>Console.ReadKey()</c>, but puts the <c>postValue</c> after the text, while typing.
        /// </summary>
        /// <param name="cursorIcon">The <c>CursorIcon</c> passed into the <c>OptionsUI</c>, that includes this object.</param>
        /// <param name="keys">The keys, that already exist.</param>
        private ConsoleKeyInfo ReadInput(CursorIcon cursorIcon, List<ConsoleKeyInfo> keys)
        {
            Console.Write("\u001b[0K");
            var preValuePos = Console.GetCursorPosition();

            foreach (var key in keys)
            {
                Console.Write(SettingsUtils.GetKeyName(key) + ", ");
            }

            var (Left, Top) = Console.GetCursorPosition();
            if (multiline)
            {
                Console.Write(postValue.Replace("\n", cursorIcon.sIconR + "\n" + cursorIcon.sIcon));
            }
            else
            {
                Console.Write(postValue);
            }
            Console.Write(cursorIcon.sIconR);

            Console.SetCursorPosition(Left, Top);
            var pressedKey = Console.ReadKey(true);
            Console.SetCursorPosition(preValuePos.Left, preValuePos.Top);
            return pressedKey;
        }
        #endregion
    }
}
