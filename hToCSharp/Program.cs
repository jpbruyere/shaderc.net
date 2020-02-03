using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace hToCSharp {
	static class Extensions {
		static Regex rxNameStartChar = new Regex (@"\p{L}|\u005F");
		static Regex rxNameChar = new Regex (@"\p{L}|\p{Nd}|\u005F");
		static Regex rxDecimal = new Regex (@"[0-9]+");
		static Regex rxHexadecimal = new Regex (@"[0-9a-fA-F]+");
		//static Regex rxNumericLiteral = new Regex (@"([0][x|X][0-9a-fA-F]+|[0-9]+[.]?[0-9]+[f]?)(?i)[u|l]{0,2}$");
		static Regex rxNumericLiteral = new Regex (@"(?i)[0-9a-fulxe]+");

		public static bool IsWhiteSpaceOrNewLine (this char c) {
			return c == '\t' || c == '\r' || c == '\n' || char.IsWhiteSpace (c);
		}
		public static bool IsValidWordStart (this char c) {
			return rxNameStartChar.IsMatch (new string (new char[] { c }));
		}
		public static bool IsValidInWord (this char c) {
			return rxNameChar.IsMatch (new string (new char[] { c }));
		}
		public static bool IsValidInNumericLiteral (this char c) {
			return rxNumericLiteral.IsMatch (new string (new char[] { c }));
		}
	}

	public class ParsingException : System.Exception {
		public int Line;
		public int Column;
		public ParsingException (int line, int column, string txt, string source = null)
			: base (txt) {
			Line = line;
			Column = column;
			Source = source;
		}
		public ParsingException (int line, int column, string txt, ParsingException innerException, string source = null)
			: base (txt, innerException) {
			Line = line;
			Column = column;
			Source = source;
		}
		public override string ToString () {
			return string.Format ("{3}:({0},{1}): {2}", Line, Column, Message, Source);
		}
	}

	public class SourceReader : StreamReader {
		public int column { get; private set; } = 1;
		public int line { get; private set; } = 1;

		public char ReadChar () {
			column++;
			return (Char)Read ();
		}
		public char PeekChar () {
			return (Char)Peek ();
		}
		public void SkipWhiteSpaceAndLineBreak () {
			while (!EndOfStream) {
				if (!PeekChar ().IsWhiteSpaceOrNewLine ())
					break;
				if (ReadChar () == '\n') {
					line++;
					column = 0;
				}
			}
		}
		public override string ReadLine () {
			line++;
			column = 0;
			return base.ReadLine ();
		}
		public string ReadUntil (string pattern) {
			int i = 0;
			StringBuilder sb = new StringBuilder ();
			while (!EndOfStream) {
				if (PeekChar () == '\n') {
					line++;
					column = 0;
				}
				sb.Append (ReadChar ());
				if (sb[sb.Length - 1] == pattern[i]) {
					if (++i == pattern.Length)
						return sb.Remove (sb.Length - pattern.Length, pattern.Length).ToString();
					continue;
				}
				i = 0;
			}
			throw new ParsingException (line, column, $"Unexpected end of file, expecting '{pattern}'");
		}
		public string ReadWord () {
			if (!PeekChar().IsValidWordStart ())
				throw new ParsingException (line, column, $"Unexpected character: '{PeekChar ()}'.");
			StringBuilder sb = new StringBuilder ();
			sb.Append (ReadChar());
			while (!EndOfStream && PeekChar ().IsValidInWord ())
				sb.Append (ReadChar ());
			return sb.ToString ();
		}
		public SourceReader (string sourcePath)
			: base (sourcePath, true) {

		}


	}
	enum TokenType {
		Comment,
		Keyword,
		Identifier,
		Punctuator,
		Operator,
		CharLiteral,
		StringLiteral,
		NumericLiteral,
		Preprocessor,
	}
	class Token : IEquatable<Token> {
		public static Token TokEnum = new Token (TokenType.Keyword, "enum");
		public static Token EndOfStatement = new Token (TokenType.Punctuator, ";");
		public static Token OpenBrace = new Token (TokenType.Keyword, "enum");

		public readonly TokenType Type;
		public readonly string Value;
		public Token (TokenType type, string value) {
			Type = type;
			Value = value;
		}
		public Token (TokenType type, char value) {
			Type = type;
			Value = new string (new char[] { value });
		}
		public override bool Equals (object obj) {
			if (!(obj is Token))
				return false;
			Token t = (Token)obj;
			return t.Type == Type && t.Value == Value;
		}

		public bool Equals (Token other) {
			return Type == other.Type &&
				   Value == other.Value;
		}

		public override int GetHashCode () {
			var hashCode = 1265339359;
			hashCode = hashCode * -1521134295 + Type.GetHashCode ();
			hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode (Value);
			return hashCode;
		}

		public static bool operator == (Token token1, Token token2) {
			return token1 is null ? token2 is null : token2 is null ? false : token1.Equals (token2);
		}

		public static bool operator != (Token token1, Token token2) {
			return !(token1 == token2);
		}
		//public static bool operator == (string token2, Token token1) {
		//	return token1.Value == token2;
		//}

		//public static bool operator != (string token2, Token token1) {
		//	return !(token1.Value == token2);
		//}
		public override string ToString () => $"{Type,-20}{Value}";

		public string ToCamelCase (int removeLeadingCharsCount = 0) {
			string[] tmp = Value.Remove (0, removeLeadingCharsCount).Split ('_');
			for (int i = 0; i < tmp.Length; i++)
				tmp[i] = tmp[i][0].ToString ().ToUpper () + tmp[i].Substring (1);
			
			return tmp.Aggregate ((a, b) => a + b);
		}
		public string ToCamelCase (string removeCommonLeadingChars) {
			int j = 0;
			while (j < removeCommonLeadingChars.Length && Value[j] == removeCommonLeadingChars[j]) 
				j++;

			while (Value[j] != '_')
				j--;

			string[] tmp = Value.Remove (0, j).Split ('_');
			for (int i = 0; i < tmp.Length; i++)
				tmp[i] = String.IsNullOrEmpty (tmp[i]) ? "" : tmp[i][0].ToString ().ToUpper () + tmp[i].Substring (1);

			return tmp.Aggregate ((a, b) => a + b);
		}
	}

	class Definition {
		public Token Name;
	}
	class EnumValueDef : Definition {
		public Token Value;
	}
	class EnumDef : Definition {
		public List<EnumValueDef> EnumValues = new List<EnumValueDef> ();
	}


	class Program {
		static string[] keywords = {
			"auto", "break", "case", "char", "const", "continue", "default", "do",
			"double", "else", "enum", "extern", "float", "for", "goto", "if", "int",
			"long", "register", "return", "short", "signed", "sizeof", "static",
			"struct", "switch", "typedef", "union", "unsigned", "void", "volatile", "while" };

		static string[] preprocessorDirectives = {
			"include", "define", "undef", "ifdef", "ifndef", "if", "else",
			"elif", "endif", "error", "pragma"
		};
		static char[] ponctuators = { ';', '{', '}', '(', ')', ',' };

		static char[] operators = {
			'+', '-', '*', '/', '%',
			'<', '>', '=', '!',
			'&', '|', '~', '^' };


		const string headerBaseDir = @"/mnt/devel/vulkan/VulkanSDK/1.1.130.0/source/shaderc/src/libshaderc/include/";
		const string headerPath = @"shaderc/shaderc.h";

		const string heading = "/* autogenerated with 'hToCSharp.net' */";


		static List<string> includePathes = new List<string> ();

		static List<Token> tokens = new List<Token> ();
		static int curTokIdx = 0;

		static Token NextTok => ++curTokIdx >= tokens.Count ? null : tokens[curTokIdx];
		static Token CurTok => curTokIdx >= tokens.Count ? null : tokens[curTokIdx];



		static void Main (string[] args) {

			using (SourceReader sr = new SourceReader (Path.Combine (headerBaseDir, headerPath))) {
				while (!sr.EndOfStream) {
					sr.SkipWhiteSpaceAndLineBreak ();
					if (sr.EndOfStream)
						break;

					char c = sr.ReadChar ();

					switch (c) {
					case '/': //comments
						switch (sr.PeekChar ()) {
						case '/': //line comment
							sr.ReadChar ();
							tokens.Add (new Token (TokenType.Comment, sr.ReadLine ()));
							break;
						case '*': //block comment
							sr.ReadChar ();
							tokens.Add (new Token (TokenType.Comment, sr.ReadUntil ("*/")));
							break;
						case '=':
							tokens.Add (new Token (TokenType.Operator, new string (new char[] { c, sr.ReadChar () })));
							break;
						default:
							tokens.Add (new Token (TokenType.Operator, c));
							break;
						}
						break;
					case '"': //string literal
						tokens.Add (new Token (TokenType.StringLiteral, sr.ReadUntil ("\"")));
						break;
					case '\'': //character literal
						tokens.Add (new Token (TokenType.CharLiteral, sr.ReadUntil ("'")));
						break;
					case '.': //dot operator
						tokens.Add (new Token (TokenType.Operator, c));
						break;
					case '#': //preprocessor directive
						string ppd = sr.ReadWord ();
						if (!preprocessorDirectives.Contains (ppd))
							throw new ParsingException (sr.line, sr.column, $"Invalid prerocessor directive: '#{ppd}'.");
						tokens.Add (new Token (TokenType.Preprocessor, ppd));
						break;
					default:
						if (ponctuators.Contains (c)) {
							tokens.Add (new Token (TokenType.Punctuator, c));
							break;
						}
						if (operators.Contains (c)) {
							if (operators.Contains (sr.PeekChar ()))
								tokens.Add (new Token (TokenType.Operator, new string (new char[] { c, sr.ReadChar () })));
							else
								tokens.Add (new Token (TokenType.Operator, c));
							break;
						}

						StringBuilder sb = new StringBuilder ();
						sb.Append (c);

						if (char.IsDigit (c)) { //numeric literal
							while (!sr.EndOfStream && sr.PeekChar ().IsValidInNumericLiteral ())
								sb.Append (sr.ReadChar ());
							tokens.Add (new Token (TokenType.NumericLiteral, sb.ToString ()));
							break;
						}

						if (!c.IsValidWordStart ())
							throw new ParsingException (sr.line, sr.column, $"Unexpected character: '{c}'.");
						while (!sr.EndOfStream && sr.PeekChar ().IsValidInWord ())
							sb.Append (sr.ReadChar ());

						string word = sb.ToString ();

						if (keywords.Contains (word))
							tokens.Add (new Token (TokenType.Keyword, word));
						else
							tokens.Add (new Token (TokenType.Identifier, word));
						break;
					}
				}
			}

			Token[] toks = tokens.Where (t => t.Type != TokenType.Comment).ToArray ();


			List<Definition> defs = new List<Definition> ();

			int i = 0;
			while (i < toks.Length) {
				if (toks[i] == Token.TokEnum) {
					i += 2; // {						
					EnumDef ed = new EnumDef ();
					while (i < toks.Length && toks[i].Value != "}") {
						if (toks[i].Value == "ifdef") {
							i += 2;
							continue;
						}
						if (toks[i].Value == "endif") {
							i ++;
							continue;
						}
						EnumValueDef ev = new EnumValueDef { Name = toks[i++] };
						if (toks[i].Value == "=") {
							ev.Value = toks[++i];
							i++;
						}
						ed.EnumValues.Add (ev);
						i++;
					}
					ed.Name = toks[++i];
					defs.Add (ed);
					i++;
				}

				i++;
			}

			string genNameSpace = "shaderc";

			using (StreamWriter sr = new StreamWriter ($"generated.cs", false, System.Text.Encoding.UTF8)) {
				using (IndentedTextWriter tw = new IndentedTextWriter (sr)) {

					tw.WriteLine (heading);
					tw.WriteLine ($"namespace {genNameSpace} {{");
					tw.Indent++;

					foreach (EnumDef ed in defs.OfType<EnumDef>()) {
						if (ed.Name.Type != TokenType.Identifier)
							break;
						tw.WriteLine ($"public enum {ed.Name.ToCamelCase (8)} {{");
						tw.Indent++;

						foreach (EnumValueDef ev in ed.EnumValues) {
							tw.Write ($"{ev.Name.ToCamelCase (ed.Name.Value)}");
							if (ev.Value == null)
								tw.WriteLine ($",");
							else {
								if (ev.Value.Type == TokenType.Identifier)
									tw.WriteLine ($" = {ev.Value.ToCamelCase(ed.Name.Value)},");
								else
									tw.WriteLine ($" = {ev.Value},");
							}
						}
						tw.Indent--;
						tw.WriteLine (@"};");
					}

					tw.Indent--;
					tw.WriteLine (@"}");

				}
			}
		}
	}
}
