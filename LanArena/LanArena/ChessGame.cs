namespace LanArena;

public struct Move
{
    public int FR, FC, TR, TC;
    public char Promo;   // '\0' = none, otherwise q / r / b / n

    public Move(int fr, int fc, int tr, int tc, char promo = '\0')
    {
        FR = fr; FC = fc; TR = tr; TC = tc; Promo = promo;
    }

    public bool Same(Move o) => FR == o.FR && FC == o.FC && TR == o.TR && TC == o.TC && Promo == o.Promo;
}

/// <summary>
/// Complete chess rules: legal moves, check, checkmate, stalemate, castling, en passant, promotion.
/// Board: B[row, col], row 0 = rank 8 (top), upper case = white, lower case = black, '.' = empty.
/// </summary>
public class ChessGame
{
    public char[,] B = new char[8, 8];
    public bool WhiteToMove = true;
    public bool HasLast;
    public Move LastMove;
    public string Result;     // null while the game is running
    public char Outcome;      // 'W' white won, 'B' black won, 'D' draw

    bool wK, wQ, bK, bQ;      // castling rights
    int epR, epC;             // en-passant target square (-1 = none)

    static readonly int[][] KnightD = { new[] { -2, -1 }, new[] { -2, 1 }, new[] { -1, -2 }, new[] { -1, 2 }, new[] { 1, -2 }, new[] { 1, 2 }, new[] { 2, -1 }, new[] { 2, 1 } };
    static readonly int[][] KingD = { new[] { -1, -1 }, new[] { -1, 0 }, new[] { -1, 1 }, new[] { 0, -1 }, new[] { 0, 1 }, new[] { 1, -1 }, new[] { 1, 0 }, new[] { 1, 1 } };
    static readonly int[][] RookD = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } };
    static readonly int[][] BishopD = { new[] { 1, 1 }, new[] { 1, -1 }, new[] { -1, 1 }, new[] { -1, -1 } };

    public ChessGame() { Reset(); }

    public void Reset()
    {
        string back = "rnbqkbnr";
        for (int c = 0; c < 8; c++)
        {
            B[0, c] = back[c];
            B[1, c] = 'p';
            B[6, c] = 'P';
            B[7, c] = char.ToUpper(back[c]);
            for (int r = 2; r < 6; r++) B[r, c] = '.';
        }
        WhiteToMove = true;
        wK = wQ = bK = bQ = true;
        epR = epC = -1;
        HasLast = false;
        Result = null;
        Outcome = '\0';
    }

    static bool In(int r, int c) => r >= 0 && r < 8 && c >= 0 && c < 8;

    // ---------- attack detection ----------
    static bool Attacked(char[,] b, int r, int c, bool byWhite)
    {
        int pr = byWhite ? r + 1 : r - 1;
        char pawn = byWhite ? 'P' : 'p';
        foreach (int dc in new[] { -1, 1 })
            if (In(pr, c + dc) && b[pr, c + dc] == pawn) return true;

        char kn = byWhite ? 'N' : 'n';
        foreach (var d in KnightD)
            if (In(r + d[0], c + d[1]) && b[r + d[0], c + d[1]] == kn) return true;

        char kg = byWhite ? 'K' : 'k';
        foreach (var d in KingD)
            if (In(r + d[0], c + d[1]) && b[r + d[0], c + d[1]] == kg) return true;

        char bi = byWhite ? 'B' : 'b', ro = byWhite ? 'R' : 'r', qu = byWhite ? 'Q' : 'q';
        foreach (var d in BishopD)
        {
            int rr = r + d[0], cc = c + d[1];
            while (In(rr, cc))
            {
                char p = b[rr, cc];
                if (p != '.') { if (p == bi || p == qu) return true; break; }
                rr += d[0]; cc += d[1];
            }
        }
        foreach (var d in RookD)
        {
            int rr = r + d[0], cc = c + d[1];
            while (In(rr, cc))
            {
                char p = b[rr, cc];
                if (p != '.') { if (p == ro || p == qu) return true; break; }
                rr += d[0]; cc += d[1];
            }
        }
        return false;
    }

    static (int r, int c) FindKing(char[,] b, bool white)
    {
        char k = white ? 'K' : 'k';
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
                if (b[r, c] == k) return (r, c);
        return (-1, -1);
    }

    public (int r, int c) KingSquare(bool white) => FindKing(B, white);

    public bool InCheck(bool white)
    {
        var k = FindKing(B, white);
        return k.r >= 0 && Attacked(B, k.r, k.c, !white);
    }

    // ---------- move generation ----------
    void AddPawn(List<Move> l, int r, int c, int nr, int nc, int lastRow)
    {
        if (nr == lastRow) foreach (char pc in "qrbn") l.Add(new Move(r, c, nr, nc, pc));
        else l.Add(new Move(r, c, nr, nc));
    }

    void Slide(List<Move> l, int r, int c, int[][] dirs, bool white)
    {
        foreach (var d in dirs)
        {
            int rr = r + d[0], cc = c + d[1];
            while (In(rr, cc))
            {
                char t = B[rr, cc];
                if (t == '.') l.Add(new Move(r, c, rr, cc));
                else { if (char.IsUpper(t) != white) l.Add(new Move(r, c, rr, cc)); break; }
                rr += d[0]; cc += d[1];
            }
        }
    }

    List<Move> Pseudo(int r, int c)
    {
        var l = new List<Move>();
        char p = B[r, c];
        if (p == '.') return l;
        bool w = char.IsUpper(p);

        switch (char.ToLower(p))
        {
            case 'p':
            {
                int dir = w ? -1 : 1, start = w ? 6 : 1, last = w ? 0 : 7, nr = r + dir;
                if (In(nr, c) && B[nr, c] == '.')
                {
                    AddPawn(l, r, c, nr, c, last);
                    if (r == start && B[r + 2 * dir, c] == '.') l.Add(new Move(r, c, r + 2 * dir, c));
                }
                foreach (int dc in new[] { -1, 1 })
                {
                    int nc = c + dc;
                    if (!In(nr, nc)) continue;
                    char t = B[nr, nc];
                    if (t != '.' && char.IsUpper(t) != w) AddPawn(l, r, c, nr, nc, last);
                    else if (t == '.' && nr == epR && nc == epC) l.Add(new Move(r, c, nr, nc));
                }
                break;
            }
            case 'n':
                foreach (var d in KnightD)
                {
                    int nr = r + d[0], nc = c + d[1];
                    if (In(nr, nc) && (B[nr, nc] == '.' || char.IsUpper(B[nr, nc]) != w)) l.Add(new Move(r, c, nr, nc));
                }
                break;
            case 'b': Slide(l, r, c, BishopD, w); break;
            case 'r': Slide(l, r, c, RookD, w); break;
            case 'q': Slide(l, r, c, BishopD, w); Slide(l, r, c, RookD, w); break;
            case 'k':
            {
                foreach (var d in KingD)
                {
                    int nr = r + d[0], nc = c + d[1];
                    if (In(nr, nc) && (B[nr, nc] == '.' || char.IsUpper(B[nr, nc]) != w)) l.Add(new Move(r, c, nr, nc));
                }
                int row = w ? 7 : 0;
                if (r == row && c == 4)
                {
                    bool ks = w ? wK : bK, qs = w ? wQ : bQ;
                    char rook = w ? 'R' : 'r';
                    if (ks && B[row, 5] == '.' && B[row, 6] == '.' && B[row, 7] == rook &&
                        !Attacked(B, row, 4, !w) && !Attacked(B, row, 5, !w) && !Attacked(B, row, 6, !w))
                        l.Add(new Move(row, 4, row, 6));
                    if (qs && B[row, 3] == '.' && B[row, 2] == '.' && B[row, 1] == '.' && B[row, 0] == rook &&
                        !Attacked(B, row, 4, !w) && !Attacked(B, row, 3, !w) && !Attacked(B, row, 2, !w))
                        l.Add(new Move(row, 4, row, 2));
                }
                break;
            }
        }
        return l;
    }

    static void Apply(char[,] b, Move m)
    {
        char p = b[m.FR, m.FC];
        // en passant capture
        if (char.ToLower(p) == 'p' && m.FC != m.TC && b[m.TR, m.TC] == '.') b[m.FR, m.TC] = '.';
        // castling: move the rook too
        if (char.ToLower(p) == 'k' && Math.Abs(m.TC - m.FC) == 2)
        {
            int rr = m.FR;
            if (m.TC == 6) { b[rr, 5] = b[rr, 7]; b[rr, 7] = '.'; }
            else { b[rr, 3] = b[rr, 0]; b[rr, 0] = '.'; }
        }
        b[m.TR, m.TC] = p;
        b[m.FR, m.FC] = '.';
        if (m.Promo != '\0') b[m.TR, m.TC] = char.IsUpper(p) ? char.ToUpper(m.Promo) : char.ToLower(m.Promo);
    }

    public List<Move> LegalMoves()
    {
        var res = new List<Move>();
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
            {
                char p = B[r, c];
                if (p == '.' || char.IsUpper(p) != WhiteToMove) continue;
                foreach (var m in Pseudo(r, c))
                {
                    var nb = (char[,])B.Clone();
                    Apply(nb, m);
                    var k = FindKing(nb, WhiteToMove);
                    if (k.r >= 0 && !Attacked(nb, k.r, k.c, !WhiteToMove)) res.Add(m);
                }
            }
        return res;
    }

    void ClearRights(int r, int c)
    {
        if (r == 7 && c == 0) wQ = false;
        if (r == 7 && c == 7) wK = false;
        if (r == 0 && c == 0) bQ = false;
        if (r == 0 && c == 7) bK = false;
        if (r == 7 && c == 4) { wK = wQ = false; }
        if (r == 0 && c == 4) { bK = bQ = false; }
    }

    /// <summary>Plays the move only if it is legal. Returns false otherwise.</summary>
    public bool TryMake(Move m)
    {
        if (Result != null) return false;
        foreach (var lm in LegalMoves())
            if (lm.Same(m)) { MakeMove(lm); return true; }
        return false;
    }

    public void MakeMove(Move m)
    {
        char p = B[m.FR, m.FC];
        ClearRights(m.FR, m.FC);
        ClearRights(m.TR, m.TC);
        epR = epC = -1;
        if (char.ToLower(p) == 'p' && Math.Abs(m.TR - m.FR) == 2) { epR = (m.FR + m.TR) / 2; epC = m.FC; }

        Apply(B, m);
        LastMove = m;
        HasLast = true;
        WhiteToMove = !WhiteToMove;
        Evaluate();
    }

    void Evaluate()
    {
        var moves = LegalMoves();
        if (moves.Count == 0)
        {
            if (InCheck(WhiteToMove))
            {
                Outcome = WhiteToMove ? 'B' : 'W';
                Result = WhiteToMove ? "Checkmate - Black wins" : "Checkmate - White wins";
            }
            else { Outcome = 'D'; Result = "Stalemate - draw"; }
        }
        else if (Insufficient()) { Outcome = 'D'; Result = "Draw - not enough material"; }
    }

    bool Insufficient()
    {
        var others = new List<char>();
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
            {
                char p = char.ToLower(B[r, c]);
                if (p != '.' && p != 'k') others.Add(p);
            }
        return others.Count == 0 || (others.Count == 1 && (others[0] == 'b' || others[0] == 'n'));
    }
}
