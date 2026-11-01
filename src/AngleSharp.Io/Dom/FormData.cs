namespace AngleSharp.Io.Dom
{
    using AngleSharp.Attributes;
    using AngleSharp.Dom;
    using AngleSharp.Html;
    using AngleSharp.Html.Dom;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// The entries of a form (or set by script) to send with fetch or
    /// XMLHttpRequest as multipart/form-data.
    /// </summary>
    [DomName("FormData")]
    public sealed class FormData
    {
        private readonly List<KeyValuePair<String, String>> _entries = new List<KeyValuePair<String, String>>();

        /// <summary>
        /// Creates the entries, taken from the controls of the given form, if any.
        /// </summary>
        /// <param name="form">The form to take the entries from.</param>
        [DomConstructor]
        public FormData(IHtmlFormElement form = null)
        {
            if (form != null)
            {
                foreach (var element in form.Elements)
                {
                    AddEntries(element);
                }
            }
        }

        /// <summary>
        /// Adds an entry.
        /// </summary>
        /// <param name="name">The name of the entry.</param>
        /// <param name="value">The value of the entry.</param>
        [DomName("append")]
        public void Append(String name, String value) =>
            _entries.Add(new KeyValuePair<String, String>(name, value ?? String.Empty));

        /// <summary>
        /// Replaces the entries of the name with the given value.
        /// </summary>
        /// <param name="name">The name of the entry.</param>
        /// <param name="value">The value of the entry.</param>
        [DomName("set")]
        public void Set(String name, String value)
        {
            var index = _entries.FindIndex(entry => entry.Key == name);

            if (index < 0)
            {
                Append(name, value);
            }
            else
            {
                // the first entry takes the value, the others go
                _entries[index] = new KeyValuePair<String, String>(name, value ?? String.Empty);

                for (var i = _entries.Count - 1; i > index; i--)
                {
                    if (_entries[i].Key == name)
                    {
                        _entries.RemoveAt(i);
                    }
                }
            }
        }

        /// <summary>
        /// Gets the value of the first entry of the name.
        /// </summary>
        /// <param name="name">The name of the entry.</param>
        /// <returns>The value or null if there is no such entry.</returns>
        [DomName("get")]
        public String Get(String name) =>
            _entries.Where(entry => entry.Key == name).Select(entry => entry.Value).FirstOrDefault();

        /// <summary>
        /// Gets the values of all entries of the name.
        /// </summary>
        /// <param name="name">The name of the entries.</param>
        /// <returns>The values in the order of the entries.</returns>
        [DomName("getAll")]
        public String[] GetAll(String name) =>
            _entries.Where(entry => entry.Key == name).Select(entry => entry.Value).ToArray();

        /// <summary>
        /// Gets if there is an entry of the name.
        /// </summary>
        /// <param name="name">The name of the entry.</param>
        /// <returns>True if there is such an entry.</returns>
        [DomName("has")]
        public Boolean Has(String name) => _entries.Any(entry => entry.Key == name);

        /// <summary>
        /// Removes all entries of the name.
        /// </summary>
        /// <param name="name">The name of the entries.</param>
        [DomName("delete")]
        public void Delete(String name) => _entries.RemoveAll(entry => entry.Key == name);

        /// <summary>
        /// Creates the data set to send for the entries.
        /// </summary>
        /// <returns>The data set with all entries.</returns>
        public FormDataSet ToDataSet()
        {
            var dataSet = new FormDataSet();

            foreach (var entry in _entries)
            {
                dataSet.Append(entry.Key, entry.Value, "text");
            }

            return dataSet;
        }

        //  A simplified "constructing the entry list" (HTML 4.10.21.4) - named, enabled controls,
        //  checked boxes and the selected options; no files.
        private void AddEntries(IElement element)
        {
            var name = element.GetAttribute(AttributeNames.Name);

            if (String.IsNullOrEmpty(name) || element.HasAttribute(AttributeNames.Disabled))
            {
                return;
            }

            switch (element)
            {
                case IHtmlInputElement input:
                    var type = input.Type?.ToLowerInvariant();

                    if (type is "submit" or "button" or "reset" or "image" or "file")
                    {
                        return;
                    }
                    else if ((type is "checkbox" or "radio") && !input.IsChecked)
                    {
                        return;
                    }

                    Append(name, (type is "checkbox" or "radio") && !input.HasAttribute(AttributeNames.Value) ? "on" : input.Value);
                    break;
                case IHtmlSelectElement select:
                    foreach (var option in select.Options.Where(option => option.IsSelected && !option.IsDisabled))
                    {
                        Append(name, option.Value);
                    }

                    break;
                case IHtmlTextAreaElement textArea:
                    Append(name, textArea.Value);
                    break;
            }
        }
    }
}
