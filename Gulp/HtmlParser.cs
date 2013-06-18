using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gulp
{
    internal class HtmlParser
    {
        public class HtmlTag
        {
            public string Name { get; set; }
            public Dictionary<string, string> Attributes { get; set; }
            public bool TrailingSlash { get; set; }
        }

        protected string html;
        protected int index;
        protected bool scriptBegin;

        public HtmlParser(string html)
        {
            Reset(html);
        }

        public void Reset()
        {
            index = 0;
        }

        public void Reset(string html)
        {
            this.html = html;

            index = 0;
        }

        public bool EOF
        {
            get
            {
                return (index >= html.Length);
            }
        }

        public bool ParseNext(Func<string, bool> isTagName, out HtmlTag tag)
        {
            const string ENDCOMMENT = "-->";

            tag = null;

            while (MoveToNextTag())
            {
                Move();

                char c = Peek();

                if (c == '!' && Peek(1) == '-' && Peek(2) == '-')
                {
                    index = html.IndexOf(ENDCOMMENT, index);

                    NormalizePosition();

                    Move(ENDCOMMENT.Length);
                }
                else if (c == '/')
                {
                    index = html.IndexOf('>', index);

                    NormalizePosition();

                    Move();
                }
                else
                {
                    bool result = ParseTag(isTagName, ref tag);

                    if (scriptBegin)
                    {
                        const string endScript = "</script";

                        index = html.IndexOf(endScript,
                            index, StringComparison.OrdinalIgnoreCase);

                        NormalizePosition();

                        Move(endScript.Length);

                        SkipWhitespace();

                        if (Peek() == '>')
                            Move();
                    }

                    if (result)
                        return true;
                }
            }

            return false;
        }

        protected bool ParseTag(Func<string, bool> isTagName, ref HtmlTag tag)
        {
            var s = ParseTagName();

            var doctype = scriptBegin = false;

            if (string.Compare(s, "!DOCTYPE", true) == 0)
                doctype = true;
            else if (string.Compare(s, "script", true) == 0)
                scriptBegin = true;

            bool requested = false;

            if (isTagName(s))
            {
                tag = new HtmlTag();

                tag.Name = s;

                tag.Attributes = new Dictionary<string, string>();

                requested = true;
            }

            SkipWhitespace();

            while (Peek() != '>')
            {
                if (Peek() == '/')
                {
                    if (requested)
                        tag.TrailingSlash = true;

                    Move();

                    SkipWhitespace();

                    scriptBegin = false;
                }
                else
                {
                    s = (!doctype) ? ParseAttributeName() : ParseAttributeValue();

                    SkipWhitespace();

                    string value = string.Empty;

                    if (Peek() == '=')
                    {
                        Move();

                        SkipWhitespace();

                        value = ParseAttributeValue();

                        SkipWhitespace();
                    }

                    if (requested)
                    {
                        if (tag.Attributes.Keys.Contains(s))
                            tag.Attributes.Remove(s);

                        tag.Attributes.Add(s, value);
                    }
                }
            }

            Move();

            return requested;
        }

        protected string ParseTagName()
        {
            int start = index;

            while (!EOF && !Char.IsWhiteSpace(Peek()) && Peek() != '>')
                Move();

            return html.Substring(start, index - start).ToLower();
        }

        protected string ParseAttributeName()
        {
            int start = index;

            while (!EOF && !Char.IsWhiteSpace(Peek()) && Peek() != '>' && Peek() != '=')
                Move();

            return html.Substring(start, index - start);
        }

        protected string ParseAttributeValue()
        {
            int start;
            int end;

            char c = Peek();

            if (c == '"' || c == '\'')
            {
                Move();

                start = index;

                index = html.IndexOfAny(new char[] { c, '\r', '\n' }, start);

                NormalizePosition();

                end = index;

                if (Peek() == c)
                    Move();
            }
            else
            {
                start = index;

                while (!EOF && !Char.IsWhiteSpace(c) && c != '>')
                {
                    Move();

                    c = Peek();
                }

                end = index;
            }

            return html.Substring(start, end - start).ToLower();
        }

        protected bool MoveToNextTag()
        {
            index = html.IndexOf('<', index);

            NormalizePosition();

            return !EOF;
        }

        public char Peek()
        {
            return Peek(0);
        }

        public char Peek(int ahead)
        {
            int pos = (index + ahead);

            if (pos < html.Length)
                return html[pos];

            return (char)0;
        }

        protected void Move()
        {
            Move(1);
        }

        protected void Move(int ahead)
        {
            index = Math.Min(index + ahead, html.Length);
        }

        protected void SkipWhitespace()
        {
            while (!EOF && Char.IsWhiteSpace(Peek()))
                Move();
        }

        protected void NormalizePosition()
        {
            if (index < 0)
                index = html.Length;
        }
    }
}
