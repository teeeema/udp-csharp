#!/usr/bin/env python3
"""
Generate a comprehensive, detailed technical PDF manual on the code and Unity implementation
for Practice 4: Client-Side Prediction, Server Reconciliation, and Network Synchronization.
"""
from pathlib import Path
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.lib import colors
from reportlab.lib.pagesizes import A4
from reportlab.lib.units import mm
from reportlab.lib.styles import ParagraphStyle
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle, Image, HRFlowable, PageBreak
)
from reportlab.pdfgen import canvas
import pypdf

# -------------------------------------------------------------------------
# FONT SETUP
# -------------------------------------------------------------------------
FONT_REGULAR = '/System/Library/Fonts/Supplemental/Arial.ttf'
FONT_BOLD = '/System/Library/Fonts/Supplemental/Arial Bold.ttf'
FONT_ITALIC = '/System/Library/Fonts/Supplemental/Arial Italic.ttf'
FONT_BOLD_ITALIC = '/System/Library/Fonts/Supplemental/Arial Bold Italic.ttf'

pdfmetrics.registerFont(TTFont('Arial', FONT_REGULAR))
pdfmetrics.registerFont(TTFont('Arial-Bold', FONT_BOLD))
pdfmetrics.registerFont(TTFont('Arial-Italic', FONT_ITALIC))
pdfmetrics.registerFont(TTFont('Arial-BoldItalic', FONT_BOLD_ITALIC))
pdfmetrics.registerFontFamily('Arial', normal='Arial', bold='Arial-Bold', italic='Arial-Italic', boldItalic='Arial-BoldItalic')

# -------------------------------------------------------------------------
# COLOR PALETTE
# -------------------------------------------------------------------------
PRIMARY = colors.HexColor('#0F2942')       # Deep Navy
SECONDARY = colors.HexColor('#1E5AA8')     # Corporate Blue
ACCENT = colors.HexColor('#059669')        # Emerald Green
DARK_TEXT = colors.HexColor('#1E293B')     # Slate 800
BODY_TEXT = colors.HexColor('#334155')     # Slate 700
LIGHT_BG = colors.HexColor('#F8FAFC')      # Slate 50
CARD_BG = colors.HexColor('#F1F5F9')       # Slate 100
CODE_BG = colors.HexColor('#0F172A')       # Dark Code BG
CODE_TEXT = colors.HexColor('#E2E8F0')     # Code Text
BORDER_COL = colors.HexColor('#CBD5E1')    # Slate 300

# -------------------------------------------------------------------------
# STYLES
# -------------------------------------------------------------------------
styles = {
    'CoverTitle': ParagraphStyle('CoverTitle', fontName='Arial-Bold', fontSize=18, leading=22, textColor=PRIMARY, alignment=1, spaceAfter=4),
    'CoverSub': ParagraphStyle('CoverSub', fontName='Arial', fontSize=9.5, leading=13.5, textColor=BODY_TEXT, alignment=1, spaceAfter=8),
    'CoverMeta': ParagraphStyle('CoverMeta', fontName='Arial', fontSize=8, leading=11, textColor=SECONDARY, alignment=1),
    
    'H1': ParagraphStyle('H1', fontName='Arial-Bold', fontSize=11.5, leading=14.5, textColor=PRIMARY, spaceBefore=7, spaceAfter=3, keepWithNext=True),
    'H2': ParagraphStyle('H2', fontName='Arial-Bold', fontSize=9.5, leading=12.5, textColor=SECONDARY, spaceBefore=5, spaceAfter=2, keepWithNext=True),
    
    'Body': ParagraphStyle('Body', fontName='Arial', fontSize=7.8, leading=10.6, textColor=BODY_TEXT, spaceAfter=2.5),
    'BodyBold': ParagraphStyle('BodyBold', fontName='Arial-Bold', fontSize=7.8, leading=10.6, textColor=DARK_TEXT, spaceAfter=2.5),
    
    'CodeSnippet': ParagraphStyle('CodeSnippet', fontName='Arial', fontSize=6.8, leading=8.8, textColor=CODE_TEXT),
    
    'TableHead': ParagraphStyle('TableHead', fontName='Arial-Bold', fontSize=7.5, leading=9.5, textColor=colors.white, alignment=1),
    'TableCell': ParagraphStyle('TableCell', fontName='Arial', fontSize=7.2, leading=9.2, textColor=DARK_TEXT),
    'TableCellBold': ParagraphStyle('TableCellBold', fontName='Arial-Bold', fontSize=7.2, leading=9.2, textColor=DARK_TEXT),
    'TableCellCode': ParagraphStyle('TableCellCode', fontName='Arial', fontSize=6.8, leading=8.8, textColor=colors.HexColor('#0F172A')),
    
    'CardText': ParagraphStyle('CardText', fontName='Arial', fontSize=7.3, leading=9.6, textColor=DARK_TEXT),
    'CardTitle': ParagraphStyle('CardTitle', fontName='Arial-Bold', fontSize=8, leading=10.5, textColor=PRIMARY, spaceAfter=1.5),
    
    'QTitle': ParagraphStyle('QTitle', fontName='Arial-Bold', fontSize=8.5, leading=11, textColor=PRIMARY, keepWithNext=True),
    'QAnswer': ParagraphStyle('QAnswer', fontName='Arial', fontSize=7.5, leading=10, textColor=BODY_TEXT),
    'QShort': ParagraphStyle('QShort', fontName='Arial-Bold', fontSize=7.5, leading=10, textColor=ACCENT),
}

# -------------------------------------------------------------------------
# CANVAS WITH NUMBERING & HEADERS/FOOTERS
# -------------------------------------------------------------------------
class NumberedCanvas(canvas.Canvas):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self._saved_page_states = []

    def showPage(self):
        self._saved_page_states.append(dict(self.__dict__))
        self._startPage()

    def save(self):
        num_pages = len(self._saved_page_states)
        for state in self._saved_page_states:
            self.__dict__.update(state)
            self.draw_header_footer(num_pages)
            super().showPage()
        super().save()

    def draw_header_footer(self, page_count):
        self.saveState()
        self.setFont('Arial', 7.5)
        self.setFillColor(colors.HexColor('#64748B'))
        
        # Header (pages > 1)
        if self._pageNumber > 1:
            self.drawString(14*mm, 285*mm, "Практическая работа №4: Архитектура кода, алгоритмы CSP и реализация в Unity")
            self.drawRightString(196*mm, 285*mm, "Unity 6 + NGO 2.13.3")
            self.setStrokeColor(BORDER_COL)
            self.setLineWidth(0.5)
            self.line(14*mm, 283*mm, 196*mm, 283*mm)
            
        # Footer
        page_text = f"Стр. {self._pageNumber} из {page_count}"
        self.drawRightString(196*mm, 10*mm, page_text)
        self.drawString(14*mm, 10*mm, "Разработка многопользовательских приложений VR | Кафедра ВТ / ИТ")
        self.setStrokeColor(BORDER_COL)
        self.setLineWidth(0.5)
        self.line(14*mm, 12.5*mm, 196*mm, 12.5*mm)
        self.restoreState()

def create_code_box(code_text: str):
    p = Paragraph(code_text.replace('\n', '<br/>').replace(' ', '&nbsp;'), styles['CodeSnippet'])
    t = Table([[p]], colWidths=[182*mm])
    t.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,-1), CODE_BG),
        ('BOX', (0,0), (-1,-1), 0.5, colors.HexColor('#334155')),
        ('TOPPADDING', (0,0), (-1,-1), 3),
        ('BOTTOMPADDING', (0,0), (-1,-1), 3),
        ('LEFTPADDING', (0,0), (-1,-1), 5),
        ('RIGHTPADDING', (0,0), (-1,-1), 5),
    ]))
    return t

def create_callout(title: str, text: str, border_color=SECONDARY, bg_color=CARD_BG):
    content = [
        Paragraph(f"<b>{title}</b>", styles['CardTitle']),
        Paragraph(text, styles['CardText'])
    ]
    t = Table([[content]], colWidths=[182*mm])
    t.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,-1), bg_color),
        ('BOX', (0,0), (-1,-1), 0.6, border_color),
        ('TOPPADDING', (0,0), (-1,-1), 3),
        ('BOTTOMPADDING', (0,0), (-1,-1), 3),
        ('LEFTPADDING', (0,0), (-1,-1), 5),
        ('RIGHTPADDING', (0,0), (-1,-1), 5),
    ]))
    return t

# -------------------------------------------------------------------------
# DOCUMENT BUILDER
# -------------------------------------------------------------------------
def generate_pdf(output_path: Path):
    doc = SimpleDocTemplate(
        str(output_path),
        pagesize=A4,
        leftMargin=14*mm,
        rightMargin=14*mm,
        topMargin=15*mm,
        bottomMargin=15*mm
    )
    
    media_dir = Path(__file__).resolve().parent.parent / "docs" / "media"
    img_tank = str(media_dir / "tank_prediction_demo.png")
    img_baseline = str(media_dir / "baseline_no_prediction.png")
    img_prediction = str(media_dir / "prediction_enabled.png")
    img_correction = str(media_dir / "correction_example.png")
    
    story = []
    
    # =========================================================================
    # СТРАНИЦА 1: ВВЕДЕНИЕ, ТЕОРИЯ, СРАВНЕНИЕ МОДЕЛЕЙ И ФОТО ПОЛИГОНА
    # =========================================================================
    story.append(Paragraph("МЕТОДИЧЕСКОЕ РУКОВОДСТВО ПО КОДУ И АРХИТЕКТУРЕ UNITY", styles['CoverTitle']))
    story.append(Paragraph(
        "<b>Практическая работа №4:</b> Реализация алгоритмов Client-Side Prediction, Server Reconciliation и "
        "интерполяции сущностей в многопользовательском танковом симуляторе на базе Unity NGO", 
        styles['CoverSub']
    ))
    story.append(Paragraph(
        "<b>Стек технологий:</b> Unity 6 (6000.3.8f1) • Unity Netcode for GameObjects (NGO 2.13.3) • Unity Transport (UTP) • C# .NET Standard 2.1<br/>"
        "<b>Архитектурный шаблон:</b> Авторитетный сервер (Server Authority) с буферизацией сохранённых ходов (Saved Moves Buffer)",
        styles['CoverMeta']
    ))
    story.append(HRFlowable(width="100%", thickness=1.2, color=PRIMARY, spaceAfter=5, spaceBefore=3))
    
    story.append(Paragraph("1. Теоретический базис и проблематика сетевой синхронизации", styles['H1']))
    story.append(Paragraph(
        "В сетевых играх реального времени (VR/AR, сетевые симуляторы, шутеры) критически важно устранить ощущение задержки "
        "(input lag), не жертвуя защитой от читерства. Существуют три ключевые сетевые парадигмы:",
        styles['Body']
    ))
    
    models_data = [
        [Paragraph("Сетевая модель", styles['TableHead']), Paragraph("Принцип работы", styles['TableHead']), Paragraph("Преимущества", styles['TableHead']), Paragraph("Недостатки", styles['TableHead'])],
        [
            Paragraph("<b>Peer-to-Peer / Lockstep</b>", styles['TableCellBold']),
            Paragraph("Каждый узел обменивается вводом; все ждут самый медленный узел.", styles['TableCell']),
            Paragraph("Минимальный серверный трафик.", styles['TableCell']),
            Paragraph("Лаг пропорционален худшему пингу; игра замирает при сбоях.", styles['TableCell'])
        ],
        [
            Paragraph("<b>Client Authority (Клиент-владелец)</b>", styles['TableCellBold']),
            Paragraph("Клиент сам вычисляет позицию (X,Y,Z) и отправляет её серверу.", styles['TableCell']),
            Paragraph("Нулевая задержка отклика.", styles['TableCell']),
            Paragraph("<font color='#DC2626'><b>Полная уязвимость:</b> читы (SpeedHack, NoClip, телепортация).</font>", styles['TableCell'])
        ],
        [
            Paragraph("<b>Server Authority + CSP + Reconciliation (Наш выбор)</b>", styles['TableCellBold']),
            Paragraph("Сервер — единственный авторитет физики. Клиент шлёт ввод, предсказывает шаг и корректирует ошибки.", styles['TableCell']),
            Paragraph("<b>Абсолютная защита от читов + мгновенный отклик 0.0 мс.</b>", styles['TableCell']),
            Paragraph("Сложная архитектура: хранение очереди ходов и откат (Replay).", styles['TableCell'])
        ],
    ]
    t_models = Table(models_data, colWidths=[38*mm, 52*mm, 46*mm, 46*mm])
    t_models.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,0), PRIMARY),
        ('BOX', (0,0), (-1,-1), 0.5, BORDER_COL),
        ('INNERGRID', (0,0), (-1,-1), 0.5, BORDER_COL),
        ('ROWBACKGROUNDS', (0,1), (-1,-1), [colors.white, LIGHT_BG]),
        ('TOPPADDING', (0,0), (-1,-1), 2),
        ('BOTTOMPADDING', (0,0), (-1,-1), 2),
        ('LEFTPADDING', (0,0), (-1,-1), 3),
        ('RIGHTPADDING', (0,0), (-1,-1), 3),
    ]))
    story.append(t_models)
    story.append(Spacer(1, 3))
    
    story.append(create_callout(
        "Проблема без предсказания (Baseline Delay):",
        "При задержке в 100 мс (RTT = 200 мс) клиент без Client-Side Prediction после нажатия клавиши ждёт "
        "полный сетевой круг (RTT + Server Frame Time ≈ 210–250 мс), прежде чем объект на экране сдвинется. "
        "Управление становится вязким. CSP решает эту проблему, рассчитывая кадр на клиенте немедленно."
    ))
    story.append(Spacer(1, 3))
    
    tank_box = [
        [
            Image(img_tank, width=94*mm, height=52.8*mm),
            [
                Paragraph("<b>Прототип: Танковый полигон на Unity 6</b>", styles['CardTitle']),
                Paragraph("<b>Локальный танк (зелёный):</b> управляемый игроком боевой танк. Применяет немедленный расчет смещения (CSP) и сохраняет ввод в буфер <code>_pending</code>.", styles['CardText']),
                Paragraph("<b>Сетевой танк (песочный):</b> машина соперника, синхронизируемая через авторитетный сервер и интерполируемая на клиенте через экспоненциальное сглаживание.", styles['CardText']),
                Paragraph("<b>Объекты полигона:</b> центральный бункер, капониры и ящики боеприпасов со статическими коллайдерами для авторитетной валидации коллизий через Raycast.", styles['CardText']),
            ]
        ]
    ]
    t_tank = Table(tank_box, colWidths=[96*mm, 86*mm])
    t_tank.setStyle(TableStyle([
        ('BOX', (0,0), (-1,-1), 0.5, BORDER_COL),
        ('BACKGROUND', (0,0), (-1,-1), LIGHT_BG),
        ('VALIGN', (0,0), (-1,-1), 'MIDDLE'),
        ('TOPPADDING', (0,0), (-1,-1), 3),
        ('BOTTOMPADDING', (0,0), (-1,-1), 3),
        ('LEFTPADDING', (0,0), (-1,-1), 4),
        ('RIGHTPADDING', (0,0), (-1,-1), 4),
    ]))
    story.append(t_tank)
    
    story.append(PageBreak())
    
    # =========================================================================
    # СТРАНИЦА 2: АРХИТЕКТУРА СКРИПТОВ, PREDICTIONINPUT И PREDICTIONRULES
    # =========================================================================
    story.append(Paragraph("2. Архитектура скриптов и структура исходного кода", styles['H1']))
    story.append(Paragraph(
        "Проект разделен на изолированные модули: физика отделена от сетевого транспорта NGO, "
        "что обеспечивает 100% повторяемость расчетов как на клиенте, так и на сервере.",
        styles['Body']
    ))
    
    files_data = [
        [Paragraph("Файл и путь", styles['TableHead']), Paragraph("Класс / Модуль", styles['TableHead']), Paragraph("Назначение и зона ответственности", styles['TableHead'])],
        [
            Paragraph("<code>Assets/Scripts/Prediction/<br/>PredictionRules.cs</code>", styles['TableCellCode']),
            Paragraph("<b>PredictionRules</b><br/><i>(Static C#)</i>", styles['TableCellBold']),
            Paragraph("Чистая детерминированная физика. Расчёт смещения, нормализация ввода, проверка кулдауна рывка (1.25 с), радиус отступа коллизий.", styles['TableCell'])
        ],
        [
            Paragraph("<code>Assets/Scripts/Prediction/<br/>PredictedPlayer.cs</code>", styles['TableCellCode']),
            Paragraph("<b>PredictedPlayer</b><br/><i>(NetworkBehaviour)</i>", styles['TableCellBold']),
            Paragraph("Главный сетевой актор. Сбор ввода в <code>FixedUpdate</code>, буферизация <code>_pending</code>, отправка RPC, Reconciliation (откат и replay), плавный поворот башни и корпуса.", styles['TableCell'])
        ],
        [
            Paragraph("<code>Assets/Scripts/Prediction/<br/>NetworkBootstrap.cs</code>", styles['TableCellCode']),
            Paragraph("<b>NetworkBootstrap</b><br/><i>(MonoBehaviour)</i>", styles['TableCellBold']),
            Paragraph("Инициализация UnityTransport, переключение профилей эмуляции задержки и потерь, отрисовка масштабируемого HUD со статистикой.", styles['TableCell'])
        ],
        [
            Paragraph("<code>Assets/Scripts/Prediction/<br/>NetworkLabSettings.cs</code>", styles['TableCellCode']),
            Paragraph("<b>NetworkLabSettings</b><br/><i>(Static C#)</i>", styles['TableCellBold']),
            Paragraph("Параметры лабораторных профилей (0 ms, 100 ms, 175 ms + 5% loss). Детерминированный генератор псевдослучайных потерь пакетов.", styles['TableCell'])
        ],
        [
            Paragraph("<code>Assets/Editor/ProjectBootstrap/<br/>PredictionDemoBuilder.cs</code>", styles['TableCellCode']),
            Paragraph("<b>PredictionDemoBuilder</b><br/><i>(UnityEditor Tool)</i>", styles['TableCellBold']),
            Paragraph("Процедурная сборка префаба танка из составных геометрических блоков, материалов, освещения и арены танкового полигона.", styles['TableCell'])
        ],
    ]
    t_files = Table(files_data, colWidths=[50*mm, 38*mm, 94*mm])
    t_files.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,0), PRIMARY),
        ('BOX', (0,0), (-1,-1), 0.5, BORDER_COL),
        ('INNERGRID', (0,0), (-1,-1), 0.5, BORDER_COL),
        ('ROWBACKGROUNDS', (0,1), (-1,-1), [colors.white, LIGHT_BG]),
        ('TOPPADDING', (0,0), (-1,-1), 2),
        ('BOTTOMPADDING', (0,0), (-1,-1), 2),
        ('LEFTPADDING', (0,0), (-1,-1), 3),
        ('RIGHTPADDING', (0,0), (-1,-1), 3),
    ]))
    story.append(t_files)
    story.append(Spacer(1, 3))
    
    story.append(Paragraph("2.1. Структура данных сохранённого хода: PredictionInput", styles['H2']))
    story.append(Paragraph(
        "Для очереди сохранённых ходов (<b>Saved Moves Buffer</b>) используется неизменяемая структура <code>PredictionInput</code>. "
        "Каждое дискретное действие игрока имеет строго монотонный номер <code>Sequence</code>:",
        styles['Body']
    ))
    
    code_input = """public readonly struct PredictionInput
{
    public readonly uint Sequence;              // Монотонный идентификатор такта ввода (1, 2, 3...)
    public readonly Vector2 Move;               // Ограниченный вектор направления [-1..1, -1..1]
    public readonly bool DashRequested;         // Запрос форсированного рывка (Space)
    public readonly float LocalDashMultiplier;  // Множитель рывка (1.0 = норма, 2.0 = симуляция чита B)

    public PredictionInput(uint sequence, Vector2 move, bool dashRequested, float localDashMultiplier = 1.0f)
    {
        Sequence = sequence; Move = move; DashRequested = dashRequested; LocalDashMultiplier = localDashMultiplier;
    }
}"""
    story.append(create_code_box(code_input))
    story.append(Spacer(1, 3))
    
    story.append(Paragraph("2.2. Модуль детерминированной физики: PredictionRules.cs", styles['H2']))
    story.append(Paragraph(
        "Стандартный PhysX Unity недетерминирован из-за плавающей точки и порядка решения контактов. "
        "Поэтому физика перемещения танка вынесена в детерминированные уравнения класса <code>PredictionRules</code>:",
        styles['Body']
    ))
    
    code_rules = """public static class PredictionRules
{
    public const float TickDelta = 0.02f;           // Фиксированный шаг: 50 Гц (FixedUpdate)
    public const float MoveSpeed = 4.0f;            // Линейная скорость движения: 4.0 м/с
    public const float DashDistance = 3.0f;         // Дистанция импульсного рывка: 3.0 м
    public const float DashCooldownSeconds = 1.25f; // Кулдаун между рывками: 1.25 с
    public const float ObstacleSkin = 0.45f;        // Безопасный радиус коллизии танка

    public static Vector2 SanitizeMove(Vector2 move) => Vector2.ClampMagnitude(move, 1.0f);

    public static Vector3 ComputeDisplacement(Vector2 rawMove, bool dash, float dashMultiplier = 1.0f)
    {
        var move = SanitizeMove(rawMove);
        var direction = new Vector3(move.x, 0.0f, move.y);
        var displacement = direction * (MoveSpeed * TickDelta);
        if (dash && direction.sqrMagnitude > 0.0001f)
            displacement += direction.normalized * (DashDistance * Mathf.Max(0.0f, dashMultiplier));
        return displacement;
    }

    public static bool CanDash(float now, float nextDashTime, Vector2 rawMove)
        => now >= nextDashTime && SanitizeMove(rawMove).sqrMagnitude > 0.0001f;
}"""
    story.append(create_code_box(code_rules))
    
    story.append(PageBreak())
    
    # =========================================================================
    # СТРАНИЦА 3: СЕТЕВОЙ ПАЙПЛАЙН: FIXEDUPDATE, ОЧЕРЕДНОСТЬ И RPC
    # =========================================================================
    story.append(Paragraph("3. Пошаговый пайплайн сетевой синхронизации (Network Pipeline)", styles['H1']))
    story.append(Paragraph(
        "Рассмотрим полный жизненный цикл одного пользовательского ввода от нажатия клавиши на клавиатуре до "
        "отображения на экране локального игрока, сервера и удалённого наблюдателя.",
        styles['Body']
    ))
    
    story.append(Paragraph("Этап 1: Сбор ввода и мгновенное предсказание (Client-Side Prediction)", styles['H2']))
    story.append(Paragraph(
        "В методе <code>FixedUpdate()</code> на клиенте-владельце (<code>IsOwner</code>):<br/>"
        "1. Опрашивается новая система ввода Unity (<code>Keyboard.current</code>).<br/>"
        "2. Формируется пакет <code>PredictionInput</code> со сквозным номером <code>_nextSequence++</code>.<br/>"
        "3. Если включён CSP — немедленно вызывается <code>ApplyPredictedInput</code> (отклик <b>0.0 мс</b>).<br/>"
        "4. Ввод добавляется в буфер <code>_pending</code> (Saved Moves). Запускается корутина отправки <code>SendAfterNetworkDelay</code>.",
        styles['Body']
    ))
    
    code_fixed_update = """private void FixedUpdate()
{
    if (!IsSpawned || !IsOwner) return;

    Vector2 move = ReadKeyboardMove();
    bool dashRequested = Keyboard.current.spaceKey.wasPressedThisFrame;
    bool cheatDash = Keyboard.current.bKey.wasPressedThisFrame; // Для теста коррекции T5

    if (move.sqrMagnitude < 0.0001f && !dashRequested && !cheatDash) return;

    var input = new PredictionInput(_nextSequence++, move, dashRequested || cheatDash, cheatDash ? 2.0f : 1.0f);
    if (IsServer) {
        ProcessServerInput(input.Sequence, input.Move, input.DashRequested, NetworkLabSettings.ActiveProfile);
        return;
    }

    _pending.Add(input); // Сохраняем в очередь Saved Moves
    if (NetworkLabSettings.PredictionEnabled) {
        ApplyPredictedInput(input, updateCooldown: true); // Локальный отклик 0.0 мс!
        if (input.DashRequested) _lastDashReactionMs = 0.0f;
    }
    StartCoroutine(SendAfterNetworkDelay(input, NetworkLabSettings.ActiveProfile));
}"""
    story.append(create_code_box(code_fixed_update))
    story.append(Spacer(1, 3))
    
    story.append(Paragraph("Этап 2: Гарантия очерёдности и симуляция сетевых потерь", styles['H2']))
    story.append(Paragraph(
        "В корутине <code>SendAfterNetworkDelay</code> выдерживается задержка (<code>OneWayDelayMs</code>). "
        "Применён строгий барьер порядка, чтобы корутины соседних кадров не перепутали пакеты ввода:",
        styles['Body']
    ))
    
    code_coroutine = """private IEnumerator SendAfterNetworkDelay(PredictionInput input, LabProfile profile)
{
    var spec = NetworkLabSettings.GetProfile(profile);
    if (spec.OneWayDelayMs > 0)
        yield return new WaitForSecondsRealtime(spec.OneWayDelayMs / 1000.0f);

    // Барьер порядка: более поздний ввод перемещения никогда не обгонит рывок
    yield return new WaitUntil(() => input.Sequence == _lastSentSequence + 1);
    _lastSentSequence = input.Sequence;

    // Детерминированная проверка потери пакета (5% в профиле Lag175Loss5)
    if (NetworkLabSettings.ShouldDrop(profile, input.Sequence, ClientToServerSalt))
        yield break;

    SubmitInputRpc(input.Sequence, input.Move, input.DashRequested, (byte)profile);
}"""
    story.append(create_code_box(code_coroutine))
    story.append(Spacer(1, 3))
    
    story.append(PageBreak())
    
    # =========================================================================
    # СТРАНИЦА 4: СЕРВЕРНАЯ ВАЛИДАЦИЯ, SERVER RECONCILIATION И ИНТЕРПОЛЯЦИЯ
    # =========================================================================
    story.append(Paragraph("Этап 3: Серверная валидация и разрешение коллизий (Server Authority)", styles['H2']))
    story.append(Paragraph(
        "На сервере метод <code>SubmitInputRpc</code> передаёт параметры в <code>ProcessServerInput</code>:<br/>"
        "• Проверяется монотонность: если <code>sequence <= _serverLastSequence</code>, пакет отбрасывается.<br/>"
        "• Проверяется кулдаун рывка: сервер сравнивает текущее время со временем <code>_serverNextDashTime</code>. "
        "Читерский рывок отклоняется (<code>dashAccepted = false</code>).<br/>"
        "• Вычисляется смещение и через <code>Physics.Raycast</code> определяется допустимая дистанция до препятствия.<br/>"
        "• Сервер рассылает подтверждение всем клиентам через <code>BroadcastStateRpc</code>.",
        styles['Body']
    ))
    
    code_server = """[Rpc(SendTo.Server, Delivery = RpcDelivery.Reliable, InvokePermission = RpcInvokePermission.Owner)]
private void SubmitInputRpc(uint sequence, Vector2 move, bool dashRequested, byte profileId)
    => ProcessServerInput(sequence, move, dashRequested, (LabProfile)profileId);

private void ProcessServerInput(uint sequence, Vector2 move, bool dashRequested, LabProfile profile)
{
    if (!IsServer || sequence <= _serverLastSequence) return;
    _serverLastSequence = sequence;

    var now = Time.unscaledTime;
    var dashAccepted = dashRequested && PredictionRules.CanDash(now, _serverNextDashTime, move);
    if (dashAccepted) _serverNextDashTime = now + PredictionRules.DashCooldownSeconds;

    var displacement = PredictionRules.ComputeDisplacement(move, dashAccepted, 1.0f);
    transform.position = ResolveCollision(transform.position, displacement);

    StartCoroutine(SendStateAfterNetworkDelay(sequence, transform.position, dashAccepted, 
        Mathf.Max(0.0f, _serverNextDashTime - now), profile));
}"""
    story.append(create_code_box(code_server))
    story.append(Spacer(1, 4))
    
    # =========================================================================
    # СТРАНИЦА 4: SERVER RECONCILIATION, REPLAY, ИНТЕРПОЛЯЦИЯ И РЕЙКАСТ
    # =========================================================================
    story.append(Paragraph("4. Алгоритм Server Reconciliation (Сверка и откат расхождений)", styles['H1']))
    story.append(Paragraph(
        "Когда клиент получает от сервера подтверждение <code>BroadcastStateRpc</code>, вызывается <code>ReconcileOwner</code>. "
        "Это ключевой алгоритм синхронизации, состоящий из четырёх фаз:",
        styles['Body']
    ))
    
    story.append(Paragraph(
        "<b>1. Очистка очереди (Pruning):</b> Удаляются сохранённые шаги с <code>Sequence <= sequence</code>.<br/>"
        "<b>2. Снап к истине:</b> Позиция танка принудительно устанавливается в присланную сервером точку.<br/>"
        "<b>3. Replay (Повторный накат):</b> Поверх серверной точки повторно применяются оставшиеся шаги из <code>_pending</code>.<br/>"
        "<b>4. Анализ ошибки:</b> Если ошибка > 0.02 м, фиксируется коррекция (<code>_correctionCount++</code>).",
        styles['Body']
    ))
    
    code_reconcile = """private void ReconcileOwner(uint sequence, Vector3 authoritativePosition, bool dashAccepted, float cooldownRemaining)
{
    if (sequence <= _lastAckSequence) return;
    _lastAckSequence = sequence;
    _localNextDashTime = Time.unscaledTime + cooldownRemaining;

    // Шаг 1: Удаляем подтверждённые сервером шаги ввода
    for (var i = _pending.Count - 1; i >= 0; i--)
        if (_pending[i].Sequence <= sequence) _pending.RemoveAt(i);

    var positionBefore = transform.position;

    // Шаг 2: Устанавливаем эталонную позицию сервера
    transform.position = authoritativePosition;

    // Шаг 3: Replay — повторный накат неподтверждённых шагов
    if (NetworkLabSettings.PredictionEnabled)
        foreach (var pending in _pending)
            ApplyPredictedInput(pending, updateCooldown: false);

    // Шаг 4: Фиксация ошибки предсказания
    _lastCorrectionMeters = Vector3.Distance(positionBefore, transform.position);
    _maxCorrectionMeters = Mathf.Max(_maxCorrectionMeters, _lastCorrectionMeters);
    if (_lastCorrectionMeters > 0.02f) _correctionCount++;
}"""
    story.append(create_code_box(code_reconcile))
    story.append(Spacer(1, 3))
    
    story.append(Paragraph("4.1. Интерполяция удалённых сущностей (Remote Interpolation & Smoothing)", styles['H2']))
    story.append(Paragraph(
        "Для танков соперников (<code>!IsOwner</code>) клиент не знает ввода наперёд. Удалённый танк отображается "
        "через плавную интерполяцию к последней серверной точке с экспоненциальным затуханием <code>1.0 - exp(-k * dt)</code>:",
        styles['Body']
    ))
    
    code_interpolation = """private void Update()
{
    if (!IsSpawned || IsOwner || IsServer) return;

    // 1. Плавная интерполяция позиции (Exponential Damp)
    transform.position = Vector3.Lerp(transform.position, _remoteTarget, 1.0f - Mathf.Exp(-14.0f * Time.deltaTime));

    // 2. Плавный поворот корпуса танка по вектору смещения
    var delta = _remoteTarget - transform.position;
    if (delta.sqrMagnitude > 0.001f) {
        var targetRot = Quaternion.LookRotation(new Vector3(delta.x, 0f, delta.z));
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1.0f - Mathf.Exp(-10.0f * Time.deltaTime));
    }
}"""
    story.append(create_code_box(code_interpolation))
    story.append(Spacer(1, 3))
    
    story.append(Paragraph("4.2. Разрешение коллизий без недетерминированного PhysX", styles['H2']))
    story.append(Paragraph(
        "Вместо нестабильного выталкивания коллайдеров используется направленный луч <code>Physics.Raycast</code>. "
        "Он проверяет препятствия по ходу движения танка с защитным радиусом <code>ObstacleSkin (0.45 м)</code>:",
        styles['Body']
    ))
    
    code_raycast = """private static Vector3 ResolveCollision(Vector3 position, Vector3 displacement)
{
    var distance = displacement.magnitude;
    if (distance <= 0.0001f) return position;

    var direction = displacement / distance;
    var origin = position + Vector3.up * 0.3f; // Высота луча на уровне центра корпуса

    if (Physics.Raycast(origin, direction, out var hit, distance + PredictionRules.ObstacleSkin, ~0, QueryTriggerInteraction.Ignore))
        distance = Mathf.Max(0.0f, hit.distance - PredictionRules.ObstacleSkin);

    var result = position + direction * distance;
    result.y = 1.0f; // Фиксация высоты над поверхностью полигона
    return result;
}"""
    story.append(create_code_box(code_raycast))
    
    story.append(PageBreak())
    
    # =========================================================================
    # СТРАНИЦА 5: ИЛЛЮСТРАЦИИ ТЕСТОВ И СКРИНШОТЫ С МЕТРИКАМИ HUD
    # =========================================================================
    story.append(Paragraph("5. Иллюстрации тестов и доказательная база с замерами HUD", styles['H1']))
    story.append(Paragraph(
        "Ниже представлены снимки экранов из собранного билда проекта с реальными замерами сетевых метрик в HUD.",
        styles['Body']
    ))
    
    img_w, img_h = 100*mm, 56.25*mm
    
    # Тест T2
    card_t2 = [
        [
            Image(img_baseline, width=img_w, height=img_h),
            [
                Paragraph("<b>Тест T2: Задержка без предсказания</b>", styles['CardTitle']),
                Paragraph("<b>Параметры сети:</b> задержка 100 ms (RTT 200 ms).<br/><b>Режим:</b> <code>Prediction: OFF</code>.", styles['CardText']),
                Paragraph("<b>Показания HUD на скриншоте:</b><br/>Строка <i>«Last dash reaction»</i> фиксирует задержку <b>210.4 мс</b>!", styles['CardText']),
                Paragraph("<b>Физический смысл:</b><br/>После нажатия Space танк стоит на месте более 200 мс, пока пакет летит на сервер и возвращается подтверждение. Управление запаздывает.", styles['CardText']),
            ]
        ]
    ]
    t_t2 = Table(card_t2, colWidths=[102*mm, 80*mm])
    t_t2.setStyle(TableStyle([
        ('BOX', (0,0), (-1,-1), 0.5, BORDER_COL),
        ('BACKGROUND', (0,0), (-1,-1), LIGHT_BG),
        ('VALIGN', (0,0), (-1,-1), 'TOP'),
        ('TOPPADDING', (0,0), (-1,-1), 3),
        ('BOTTOMPADDING', (0,0), (-1,-1), 3),
        ('LEFTPADDING', (0,0), (-1,-1), 4),
        ('RIGHTPADDING', (0,0), (-1,-1), 4),
    ]))
    story.append(t_t2)
    story.append(Spacer(1, 3))
    
    # Тест T3
    card_t3 = [
        [
            Image(img_prediction, width=img_w, height=img_h),
            [
                Paragraph("<b>Тест T3: Мгновенный отклик с CSP</b>", styles['CardTitle']),
                Paragraph("<b>Параметры сети:</b> задержка 100 ms (RTT 200 ms).<br/><b>Режим:</b> <code>Prediction: ON</code>.", styles['CardText']),
                Paragraph("<b>Показания HUD на скриншоте:</b><br/>Строка <i>«Last dash reaction»</i> показывает ровно <b>0.0 мс</b>!", styles['CardText']),
                Paragraph("<b>Физический смысл:</b><br/>Танк совершает рывок в тот же физический кадр. Задержка сети полностью замаскирована для игрока, сервер подтверждает действие постфактум.", styles['CardText']),
            ]
        ]
    ]
    t_t3 = Table(card_t3, colWidths=[102*mm, 80*mm])
    t_t3.setStyle(TableStyle([
        ('BOX', (0,0), (-1,-1), 0.5, BORDER_COL),
        ('BACKGROUND', (0,0), (-1,-1), LIGHT_BG),
        ('VALIGN', (0,0), (-1,-1), 'TOP'),
        ('TOPPADDING', (0,0), (-1,-1), 3),
        ('BOTTOMPADDING', (0,0), (-1,-1), 3),
        ('LEFTPADDING', (0,0), (-1,-1), 4),
        ('RIGHTPADDING', (0,0), (-1,-1), 4),
    ]))
    story.append(t_t3)
    story.append(Spacer(1, 3))
    
    # Тест T5
    card_t5 = [
        [
            Image(img_correction, width=img_w, height=img_h),
            [
                Paragraph("<b>Тест T5: Серверная коррекция расхождения</b>", styles['CardTitle']),
                Paragraph("<b>Параметры сети:</b> 175 ms задержки + 5% потерь.<br/><b>Действие:</b> читерский рывок на клавишу <code>B</code>.", styles['CardText']),
                Paragraph("<b>Показания HUD на скриншоте:</b><br/>Зафиксирована коррекция: <i>«corrections: 1»</i>, <i>«Max reconciliation error: <b>0.535 m</b>»</i>.", styles['CardText']),
                Paragraph("<b>Физический смысл:</b><br/>Клиент попытался сдвинуться на двойную дистанцию во время серверного кулдауна. Сервер отклонил превышение. Клиент плавно скорректировал позицию без вылета игры.", styles['CardText']),
            ]
        ]
    ]
    t_t5 = Table(card_t5, colWidths=[102*mm, 80*mm])
    t_t5.setStyle(TableStyle([
        ('BOX', (0,0), (-1,-1), 0.5, BORDER_COL),
        ('BACKGROUND', (0,0), (-1,-1), LIGHT_BG),
        ('VALIGN', (0,0), (-1,-1), 'TOP'),
        ('TOPPADDING', (0,0), (-1,-1), 3),
        ('BOTTOMPADDING', (0,0), (-1,-1), 3),
        ('LEFTPADDING', (0,0), (-1,-1), 4),
        ('RIGHTPADDING', (0,0), (-1,-1), 4),
    ]))
    story.append(t_t5)
    
    story.append(PageBreak())
    
    # =========================================================================
    # СТРАНИЦА 6: ИНСТРУКЦИЯ ПО ЗАПУСКУ И КОНТРОЛЬНЫЕ ВОПРОСЫ
    # =========================================================================
    story.append(Paragraph("6. Инструкция по запуску и проведению демонстрации", styles['H1']))
    story.append(Paragraph(
        "На платформе macOS оконный менеджер блокирует повторный вызов <code>open .app</code>, если окно уже открыто. "
        "Чтобы запустить два независимых экземпляра игры (Хост и Клиент), выполните в <b>двух отдельных вкладках Терминала</b> следующую команду:",
        styles['Body']
    ))
    
    cmd_text = "/Users/tema/Documents/Разработка\\ многопользовательских\\ приложений\\ виртуальной\\ реальности/prac_1/unity/PredictionDemo/Builds/PR4PredictionDemo.app/Contents/MacOS/PredictionDemo"
    
    run_table_data = [
        [Paragraph("Окно 1 (Хост / Сервер):", styles['TableCellBold']), Paragraph(f"<code>{cmd_text}</code><br/><i>(в открывшемся окне нажмите кнопку <b>«Start Host»</b>)</i>", styles['TableCell'])],
        [Paragraph("Окно 2 (Клиент):", styles['TableCellBold']), Paragraph(f"<code>{cmd_text}</code><br/><i>(в открывшемся окне нажмите кнопку <b>«Start Client»</b>)</i>", styles['TableCell'])],
    ]
    t_run = Table(run_table_data, colWidths=[44*mm, 138*mm])
    t_run.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,-1), CARD_BG),
        ('BOX', (0,0), (-1,-1), 0.5, SECONDARY),
        ('INNERGRID', (0,0), (-1,-1), 0.5, SECONDARY),
        ('TOPPADDING', (0,0), (-1,-1), 2.5),
        ('BOTTOMPADDING', (0,0), (-1,-1), 2.5),
        ('LEFTPADDING', (0,0), (-1,-1), 4),
        ('RIGHTPADDING', (0,0), (-1,-1), 4),
    ]))
    story.append(t_run)
    story.append(Spacer(1, 3))
    
    story.append(Paragraph("Горячие клавиши и управление танком:", styles['H2']))
    story.append(Paragraph(
        "• <b>WASD / Стрелки:</b> движение танка с автоматическим разворотом корпуса по курсу.<br/>"
        "• <b>Space (Пробел):</b> легальный тактический рывок (Dash) с кулдауном 1.25 секунды.<br/>"
        "• <b>B:</b> читерский форсаж в обход кулдауна (для гарантированной демонстрации отката Server Reconciliation).<br/>"
        "• <b>Кнопка «Prediction: ON/OFF»:</b> переключение локального предсказания прямо во время игры.<br/>"
        "• <b>Кнопки профилей сети:</b> «0 ms» (LAN), «100 ms» (Интернет), «175 ms + 5% loss» (Нестабильный канал).",
        styles['Body']
    ))
    story.append(Spacer(1, 3))
    
    story.append(Paragraph("7. Контрольные вопросы преподавателя и эталонные ответы", styles['H1']))
    
    qa_list = [
        (
            "1. Почему сервер обязан быть единственным авторитетом физики (Server Authority)?",
            "Если сервер слепо принимает готовые координаты Transform от клиента, любой игрок может подменить значения памяти через CheatEngine или сфабриковать пакеты, получая мгновенную телепортацию сквозь стены (NoClip) и бесконечную скорость (SpeedHack). При Server Authority клиент передаёт лишь 'намерение' (нажатие кнопок), а сервер самостоятельно рассчитывает траекторию по законам физики.",
            "Иначе клиент может отправить любые сфабрикованные координаты и телепортироваться сквозь стены."
        ),
        (
            "2. В чём принципиальная разница между Client-Side Prediction и Server Reconciliation?",
            "Prediction (предсказание) устраняет задержку для игрока: клиент сразу двигает своего персонажа, не дожидаясь ответа сервера (шаг вслепую на 0.0 мс). Reconciliation (согласование/сверка) исправляет ошибки этого предсказания: получив эталон от сервера, клиент откатывается к серверной точке и заново проигрывает ещё не подтверждённые ходы (Replay).",
            "Prediction даёт мгновенный отклик 0.0 мс, а Reconciliation исправляет локальные расхождения по эталону сервера."
        ),
        (
            "3. Что такое очередь Saved Moves (_pending) и зачем нужен шаг повторного наката (Replay)?",
            "Когда сервер присылает подтверждение, оно относится к состоянию из прошлого (на величину RTT). Если просто телепортировать игрока на серверную координату, персонаж будет непрерывно дёргаться назад во времени. Чтобы этого не было, клиент сохраняет все отправленные, но ещё не подтверждённые шаги в очередь _pending и накатывает их поверх серверной поправки.",
            "Чтобы после серверного отката быстро проиграть ещё не подтверждённые ходы игрока и сохранить актуальное положение."
        ),
        (
            "4. Чем Network Smoothing (сетевое сглаживание) отличается от Server Reconciliation?",
            "Reconciliation — это жёсткая логическая коррекция истинных физических координат. Network Smoothing — чисто визуальный приём: оно плавно интерполирует графический меш персонажа (через Lerp/Slerp), сглаживая рывки и скрывая скачки от глаз пользователя.",
            "Reconciliation меняет физическую позицию, а Smoothing плавно анимирует модельку для глаз игрока."
        ),
    ]
    
    for q, a_full, a_short in qa_list:
        q_box = [
            Paragraph(f"<b>{q}</b>", styles['QTitle']),
            Paragraph(a_full, styles['QAnswer']),
            Paragraph(f"<b>Формулировка у доски:</b> {a_short}", styles['QShort'])
        ]
        t_q = Table([[q_box]], colWidths=[182*mm])
        t_q.setStyle(TableStyle([
            ('BACKGROUND', (0,0), (-1,-1), CARD_BG),
            ('BOX', (0,0), (-1,-1), 0.5, SECONDARY),
            ('TOPPADDING', (0,0), (-1,-1), 2),
            ('BOTTOMPADDING', (0,0), (-1,-1), 2),
            ('LEFTPADDING', (0,0), (-1,-1), 4),
            ('RIGHTPADDING', (0,0), (-1,-1), 4),
        ]))
        story.append(t_q)
        story.append(Spacer(1, 2))
        
    doc.build(story, canvasmaker=NumberedCanvas)
    print(f"Comprehensive Code Guide PDF successfully created at: {output_path}")

if __name__ == '__main__':
    root = Path(__file__).resolve().parent.parent
    target = root / "МЕТОДИЧКА_КОД_И_UNITY_ПОДРОБНАЯ.pdf"
    generate_pdf(target)
