using System;

namespace Aderant.Build.DependencyAnalyzer.TextTemplates {
    internal class Tokenizer {
        TokenizerState nextState = TokenizerState.Content;
        Location nextStateLocation;
        Location nextStateTagStartLocation;

        public Tokenizer(string fileName, string content) {
            State = TokenizerState.Content;
            this.Content = content;
            Location = nextStateLocation = nextStateTagStartLocation = new Location(fileName, 1, 1);
        }

        public TokenizerState State { get; private set; }

        public int Position { get; private set; }

        public string Content { get; }

        public string Value { get; private set; }

        public Location Location { get; private set; }
        public Location TagStartLocation { get; private set; }
        public Location TagEndLocation { get; private set; }

        public bool Advance() {
            Value = null;
            State = nextState;
            Location = nextStateLocation;
            TagStartLocation = nextStateTagStartLocation;
            if (nextState == TokenizerState.EOF) {
                return false;
            }

            nextState = GetNextStateAndCurrentValue();
            return true;
        }

        TokenizerState GetNextStateAndCurrentValue() {
            switch (State) {
                case TokenizerState.Block:
                case TokenizerState.Expression:
                case TokenizerState.Helper:
                    return GetBlockEnd();

                case TokenizerState.Directive:
                    return NextStateInDirective();

                case TokenizerState.Content:
                    return NextStateInContent();

                case TokenizerState.DirectiveName:
                    return GetDirectiveName();

                case TokenizerState.DirectiveValue:
                    return GetDirectiveValue();

                default:
                    throw new InvalidOperationException("Unexpected state '" + State + "'");
            }
        }

        TokenizerState GetBlockEnd() {
            int start = Position;
            for (; Position < Content.Length; Position++) {
                char c = Content[Position];
                nextStateTagStartLocation = nextStateLocation;
                nextStateLocation = nextStateLocation.AddCol();
                if (c == '\r') {
                    if (Position + 1 < Content.Length && Content[Position + 1] == '\n') {
                        Position++;
                    }

                    nextStateLocation = nextStateLocation.AddLine();
                } else if (c == '\n') {
                    nextStateLocation = nextStateLocation.AddLine();
                } else if (c == '>' && Content[Position - 1] == '#' && Content[Position - 2] != '\\') {
                    Value = Content.Substring(start, Position - start - 1);
                    Position++;
                    TagEndLocation = nextStateLocation;

                    //skip newlines directly after blocks, unless they're expressions
                    if (State != TokenizerState.Expression && (Position += IsNewLine()) > 0) {
                        nextStateLocation = nextStateLocation.AddLine();
                    }

                    return TokenizerState.Content;
                }
            }

            throw new ParserException("Unexpected end of file.", nextStateLocation);
        }

        TokenizerState GetDirectiveName() {
            int start = Position;
            for (; Position < Content.Length; Position++) {
                char c = Content[Position];
                if (!Char.IsLetterOrDigit(c)) {
                    Value = Content.Substring(start, Position - start);
                    return TokenizerState.Directive;
                }

                nextStateLocation = nextStateLocation.AddCol();
            }

            throw new ParserException("Unexpected end of file.", nextStateLocation);
        }

        TokenizerState GetDirectiveValue() {
            int start = Position;
            int delimiter = '\0';
            for (; Position < Content.Length; Position++) {
                char c = Content[Position];
                nextStateLocation = nextStateLocation.AddCol();
                if (c == '\r') {
                    if (Position + 1 < Content.Length && Content[Position + 1] == '\n') {
                        Position++;
                    }

                    nextStateLocation = nextStateLocation.AddLine();
                } else if (c == '\n') {
                    nextStateLocation = nextStateLocation.AddLine();
                }

                if (delimiter == '\0') {
                    if (c == '\'' || c == '"') {
                        start = Position;
                        delimiter = c;
                    } else if (!Char.IsWhiteSpace(c)) {
                        throw new ParserException("Unexpected character '" + c + "'. Expecting attribute value.", nextStateLocation);
                    }

                    continue;
                }

                if (c == delimiter) {
                    Value = Content.Substring(start + 1, Position - start - 1);
                    Position++;
                    return TokenizerState.Directive;
                }
            }

            throw new ParserException("Unexpected end of file.", nextStateLocation);
        }

        TokenizerState NextStateInContent() {
            int start = Position;
            for (; Position < Content.Length; Position++) {
                char c = Content[Position];
                nextStateTagStartLocation = nextStateLocation;
                nextStateLocation = nextStateLocation.AddCol();
                if (c == '\r') {
                    if (Position + 1 < Content.Length && Content[Position + 1] == '\n') {
                        Position++;
                    }

                    nextStateLocation = nextStateLocation.AddLine();
                } else if (c == '\n') {
                    nextStateLocation = nextStateLocation.AddLine();
                } else if (c == '<' && Position + 2 < Content.Length && Content[Position + 1] == '#') {
                    TagEndLocation = nextStateLocation;
                    char type = Content[Position + 2];
                    if (type == '@') {
                        nextStateLocation = nextStateLocation.AddCols(2);
                        Value = Content.Substring(start, Position - start);
                        Position += 3;
                        return TokenizerState.Directive;
                    }

                    if (type == '=') {
                        nextStateLocation = nextStateLocation.AddCols(2);
                        Value = Content.Substring(start, Position - start);
                        Position += 3;
                        return TokenizerState.Expression;
                    }

                    if (type == '+') {
                        nextStateLocation = nextStateLocation.AddCols(2);
                        Value = Content.Substring(start, Position - start);
                        Position += 3;
                        return TokenizerState.Helper;
                    }

                    Value = Content.Substring(start, Position - start);
                    nextStateLocation = nextStateLocation.AddCol();
                    Position += 2;
                    return TokenizerState.Block;
                }
            }

            //EOF is only valid when we're in content
            Value = Content.Substring(start);
            return TokenizerState.EOF;
        }

        int IsNewLine() {
            int found = 0;

            if (Position < Content.Length && Content[Position] == '\r') {
                found++;
            }

            if (Position + found < Content.Length && Content[Position + found] == '\n') {
                found++;
            }

            return found;
        }

        TokenizerState NextStateInDirective() {
            for (; Position < Content.Length; Position++) {
                char c = Content[Position];
                if (c == '\r') {
                    if (Position + 1 < Content.Length && Content[Position + 1] == '\n') {
                        Position++;
                    }

                    nextStateLocation = nextStateLocation.AddLine();
                } else if (c == '\n') {
                    nextStateLocation = nextStateLocation.AddLine();
                } else if (Char.IsLetter(c)) {
                    return TokenizerState.DirectiveName;
                } else if (c == '=') {
                    nextStateLocation = nextStateLocation.AddCol();
                    Position++;
                    return TokenizerState.DirectiveValue;
                } else if (c == '#' && Position + 1 < Content.Length && Content[Position + 1] == '>') {
                    Position += 2;
                    TagEndLocation = nextStateLocation.AddCols(2);
                    nextStateLocation = nextStateLocation.AddCols(3);

                    //skip newlines directly after directives
                    if ((Position += IsNewLine()) > 0) {
                        nextStateLocation = nextStateLocation.AddLine();
                    }

                    return TokenizerState.Content;
                } else if (!Char.IsWhiteSpace(c)) {
                    throw new ParserException("Directive ended unexpectedly with character '" + c + "'", nextStateLocation);
                } else {
                    nextStateLocation = nextStateLocation.AddCol();
                }
            }

            throw new ParserException("Unexpected end of file.", nextStateLocation);
        }
    }

    internal enum TokenizerState {
        Content = 0,
        Directive,
        Expression,
        Block,
        Helper,
        DirectiveName,
        DirectiveValue,
        Name,
        EOF
    }
}
