using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Web;

namespace ClassLibrary1
{
    public class Node
    {
        public char currentChar { get; set; }
        public HybridDictionary Children { get; private set; }

        public Node()
        {
            this.Children = new HybridDictionary();
        }

        public Node(char letter)
        {
            this.currentChar = letter;
            this.Children = new HybridDictionary();
        }

        public Node addChild(char character, int value)
        {
            if (Children[value] == null)
            {
                Children[value] = new Node(character);
            }
            return (Node)Children[value];
        }

    }
}