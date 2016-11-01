﻿using System.Collections.Generic;
using System.Text;

namespace Ink.Parsed
{
    internal class ContentList : Parsed.Object
    {
        public bool dontFlatten { get; set; }

        public Runtime.Container runtimeContainer {
            get {
                return (Runtime.Container) this.runtimeObject;
            }
        }

        public ContentList (List<Parsed.Object> objects)
        {
            if( objects != null )
                AddContent (objects);
        }

        public ContentList (params Parsed.Object[] objects)
        {
            if (objects != null) {
                var objList = new List<Parsed.Object> (objects);
                AddContent (objList);
            }
        }
            
        public ContentList()
        {
        }

        public void TrimTrailingWhitespace()
        {
            for (int i = this.content.Count - 1; i >= 0; --i) {
                var text = this.content [i] as Text;
                if (text == null)
                    break;

                text.text = text.text.TrimEnd (' ', '\t');
                if (text.text.Length == 0)
                    this.content.RemoveAt (i);
                else
                    break;
            }
        }

        public override Runtime.Object GenerateRuntimeObject ()
        {
            var container = new Runtime.Container ();
            if (content != null) {
                foreach (var obj in content) {
                    container.AddContent (obj.runtimeObject);
                }
            }

            if( dontFlatten )
                story.DontFlattenContainer (container);

            return container;
        }

        public override string ToString ()
        {
            var sb = new StringBuilder ();
            foreach (var c in content) {
                sb.Append (c.ToString ());
            }
            return sb.ToString ();
        }
    }
}

