using System;
using System.Collections.Generic;
using System.Linq;
using Com.AugustCellars.CoAP.Server.Resources;

namespace Com.AugustCellars.CoAP.ResourceDirectory
{
    internal class Filter
    {
        class OneFilter
        {
            public string _Key;
            public string _Value;
            public bool _Found;
            public bool _Prefix;
        }

        private readonly Dictionary<string, OneFilter> _filters = new Dictionary<string, OneFilter>();

        public int Count { get; private set; } = int.MaxValue;
        public int Page { get; private set; }

        public Filter(IEnumerable<string> uriQuery)
        {
            foreach (string x in uriQuery) {
                string[] values = x.Split('=');
                if (values[0] == "page") {
                    if (values.Length == 1) throw new Exception();
                    Page = int.Parse(values[1]);
                }
                else if (values[0] == "count") {
                    if (values.Length == 1) throw new Exception();
                    Count = int.Parse(values[1]);
                }
                else {
                    bool prefix = false;
                    string filterValue = null;
                    if (values.Length == 2) {
                        filterValue = values[1];
                        if (filterValue[0] == '"' && filterValue[filterValue.Length - 1] == '"') {
                            filterValue = filterValue.Substring(1, filterValue.Length - 2);
                        }
                        prefix = filterValue[filterValue.Length - 1] == '*';
                        if (prefix) {
                            filterValue = filterValue.Substring(0, filterValue.Length - 1);
                        }
                    }



                    _filters.Add(values[0], new OneFilter() {
                        _Key = values[0], _Value = filterValue, _Prefix = prefix
                    });
                }
            }
        }

        public bool Passes
        {
            get { return _filters.Values.All(item => item._Found); }
        }

        public void ClearState()
        {
            foreach (OneFilter item in _filters.Values) {
                item._Found = false;
            }
        }

        public bool Href(string value)
        {
            if (_filters.ContainsKey("href")) {
                if (!_filters["href"]._Found) {
                    if (_filters["href"]._Prefix) {
                        if (value.Length >= _filters["href"]._Value.Length &&
                            _filters["href"]._Value == value.Substring(0, _filters["href"]._Value.Length)) {
                            _filters["href"]._Found = true;
                            return true;
                        }
                    }
                    else {
                        if (_filters["href"]._Value == value) {
                            _filters["href"]._Found = true;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public bool Apply(ResourceAttributes attrList)
        {
            bool changed = false;

            foreach (string key in attrList.Keys) {
                if (_filters.ContainsKey(key)) {
                    if (!_filters[key]._Found) {
                        if (_filters[key]._Value == null) {
                            _filters[key]._Found = true;
                            changed = true;
                        }
                        else {
                            foreach (string v in attrList.GetValues(key)) {
                                if (_filters[key]._Prefix) {
                                    if (v.Length >= _filters[key]._Value.Length &&
                                        _filters[key]._Value == v.Substring(0, _filters[key]._Value.Length)) {
                                        _filters[key]._Found = true;
                                        changed = true;
                                        break;
                                    }
                                }
                                else {
                                    if (_filters[key]._Value == v) {
                                        _filters[key]._Found = true;
                                        changed = true;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return changed && Passes;
        }
    }
}
