#!/usr/bin/env python3
"""
Generate an illustrated, high-quality PDF guide for Practice 4 with real game screenshots.
"""
from pathlib import Path
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.lib import colors
from reportlab.lib.pagesizes import A4
from reportlab.lib.units import mm
from reportlab.lib.styles import ParagraphStyle
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle, Image, HRFlowable, PageBreak, KeepTogether
)
from reportlab.pdfgen import canvas

FONT_REGULAR = '/System/Library/Fonts/Supplemental/Arial.ttf'
FONT_BOLD = '/System/Library/Fonts/Supplemental/Arial Bold.ttf'
FONT_ITALIC = '/System/Library/Fonts/Supplemental/Arial Italic.ttf'
FONT_BOLD_ITALIC = '/System/Library/Fonts/Supplemental/Arial Bold Italic.ttf'

pdfmetrics.registerFont(TTFont('Arial', FONT_REGULAR))
pdfmetrics.registerFont(TTFont('Arial-Bold', FONT_BOLD))
pdfmetrics.registerFont(TTFont('Arial-Italic', FONT_ITALIC))
pdfmetrics.registerFont(TTFont('Arial-BoldItalic', FONT_BOLD_ITALIC))
pdfmetrics.registerFontFamily('Arial', normal='Arial', bold='Arial-Bold', italic='Arial-Italic', boldItalic='Arial-BoldItalic')

# Colors
PRIMARY = colors.HexColor('#1E3A8A')     # Navy Blue
SECONDARY = colors.HexColor('#2563EB')   # Accent Blue
DARK_TEXT = colors.HexColor('#1F2937')   # Neutral Dark
LIGHT_BG = colors.HexColor('#F8FAFC')    # Soft Light
CARD_BG = colors.HexColor('#EFF6FF')     # Light Blue Card
BORDER_COL = colors.HexColor('#CBD5E1')  # Border
GREEN_ACCENT = colors.HexColor('#047857')# Dark Green Accent
RED_ACCENT = colors.HexColor('#B91C1C')  # Red Accent

styles = {
    'Title': ParagraphStyle('Title', fontName='Arial-Bold', fontSize=18, leading=22, textColor=PRIMARY, alignment=1, spaceAfter=4),
    'Subtitle': ParagraphStyle('Subtitle', fontName='Arial', fontSize=9.5, leading=13.5, textColor=colors.HexColor('#4B5563'), alignment=1, spaceAfter=8),
    
    'H1': ParagraphStyle('H1', fontName='Arial-Bold', fontSize=12, leading=15, textColor=PRIMARY, spaceBefore=9, spaceAfter=4, keepWithNext=True),
    'H2': ParagraphStyle('H2', fontName='Arial-Bold', fontSize=10, leading=13, textColor=SECONDARY, spaceBefore=6, spaceAfter=3, keepWithNext=True),
    
    'Body': ParagraphStyle('Body', fontName='Arial', fontSize=8.3, leading=11.2, textColor=DARK_TEXT, spaceAfter=3),
    'BodyBold': ParagraphStyle('BodyBold', fontName='Arial-Bold', fontSize=8.3, leading=11.2, textColor=DARK_TEXT, spaceAfter=3),
    'Bullet': ParagraphStyle('Bullet', fontName='Arial', fontSize=8.2, leading=11, textColor=DARK_TEXT, leftIndent=10, spaceAfter=2),
    
    'CmdBlock': ParagraphStyle('CmdBlock', fontName='Arial-Bold', fontSize=7.5, leading=9.5, textColor=colors.HexColor('#0F172A')),
    'TableHead': ParagraphStyle('TableHead', fontName='Arial-Bold', fontSize=7.8, leading=10, textColor=colors.white, alignment=1),
    'TableCell': ParagraphStyle('TableCell', fontName='Arial', fontSize=7.5, leading=9.5, textColor=DARK_TEXT),
    'TableCellBold': ParagraphStyle('TableCellBold', fontName='Arial-Bold', fontSize=7.5, leading=9.5, textColor=DARK_TEXT),
    'TableCellCenter': ParagraphStyle('TableCellCenter', fontName='Arial', fontSize=7.5, leading=9.5, textColor=DARK_TEXT, alignment=1),
    
    'ImgCaption': ParagraphStyle('ImgCaption', fontName='Arial-Bold', fontSize=8, leading=10.5, textColor=PRIMARY, alignment=1, spaceBefore=3, spaceAfter=5),
    'ImgDesc': ParagraphStyle('ImgDesc', fontName='Arial', fontSize=7.5, leading=9.5, textColor=DARK_TEXT, spaceAfter=2),
    
    'QTitle': ParagraphStyle('QTitle', fontName='Arial-Bold', fontSize=8.8, leading=11.5, textColor=PRIMARY, keepWithNext=True),
    'QAnswer': ParagraphStyle('QAnswer', fontName='Arial', fontSize=8, leading=10.5, textColor=DARK_TEXT),
    'QShort': ParagraphStyle('QShort', fontName='Arial-Bold', fontSize=8, leading=10.5, textColor=GREEN_ACCENT),
}

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
            self.drawString(14*mm, 285*mm, "Иллюстрированная методичка к сдаче ПР №4 (CSP, Reconciliation, Unity)")
            self.setStrokeColor(BORDER_COL)
            self.setLineWidth(0.5)
            self.line(14*mm, 283*mm, 196*mm, 283*mm)
            
        # Footer
        page_text = f"Стр. {self._pageNumber} из {page_count}"
        self.drawRightString(196*mm, 10*mm, page_text)
        self.drawString(14*mm, 10*mm, "Разработка многопользовательских приложений VR | Практическая работа №4")
        self.setStrokeColor(BORDER_COL)
        self.setLineWidth(0.5)
        self.line(14*mm, 12.5*mm, 196*mm, 12.5*mm)
        self.restoreState()

def build_pdf(target_path: Path):
    doc = SimpleDocTemplate(
        str(target_path),
        pagesize=A4,
        leftMargin=14*mm,
        rightMargin=14*mm,
        topMargin=15*mm,
        bottomMargin=15*mm
    )
    
    media_dir = Path(__file__).resolve().parent.parent / "docs" / "media"
    img_baseline = str(media_dir / "baseline_no_prediction.png")
    img_prediction = str(media_dir / "prediction_enabled.png")
    img_correction = str(media_dir / "correction_example.png")
    
    story = []
    
    # Title
    story.append(Paragraph("Иллюстрированная методичка к сдаче ПР №4", styles['Title']))
    story.append(Paragraph(
        "<b>Тема: Client-Side Prediction, Server Reconciliation и наблюдение сетевой синхронизации</b><br/>"
        "Среда: Unity 6 (6000.3.8f1) + Netcode for GameObjects (NGO) 2.13.3 + macOS Standalone (.app)", 
        styles['Subtitle']
    ))
    story.append(HRFlowable(width="100%", thickness=1, color=PRIMARY, spaceAfter=6, spaceBefore=0))
    
    # 1. ЧТО МЫ ДЕЛАЛИ
    story.append(Paragraph("1. Что мы сделали в этой практической работе", styles['H1']))
    story.append(Paragraph(
        "Мы разработали интерактивный сетевой прототип на <b>Unity</b> для двух игроков, работающий по модели <b>«сервер — авторитет» (Server Authority)</b>. "
        "Проект наглядно демонстрирует проблему сетевой задержки (lag) и решает её с помощью двух ключевых алгоритмов сетевых игр: "
        "<b>Client-Side Prediction (CSP)</b> — мгновенного локального предсказания ввода, и <b>Server Reconciliation</b> — авторитетной серверной коррекции расхождений.",
        styles['Body']
    ))
    
    roles_data = [
        [Paragraph("Компонент", styles['TableHead']), Paragraph("Роль в архитектуре проекта", styles['TableHead'])],
        [Paragraph("<code>NetworkBootstrap</code>", styles['TableCellBold']), Paragraph("Старт Host/Client, отображение HUD, интерактивное переключение сетевых профилей (0 мс, 100 мс, 175 мс) и тумблера Prediction.", styles['TableCell'])],
        [Paragraph("<code>PredictedPlayer</code>", styles['TableCellBold']), Paragraph("Сбор локального ввода, мгновенное предсказание (CSP), буферизация <code>_pending</code>, отправка RPC, приём ACK и откат при ошибке (Reconciliation).", styles['TableCell'])],
        [Paragraph("<code>PredictionRules</code>", styles['TableCellBold']), Paragraph("Авторитетная физика: нормализация вектора движения, проверка кулдауна рывка (1.25 с), расчёт смещения и предотвращение прохождения сквозь стены.", styles['TableCell'])],
        [Paragraph("<code>NetworkLabSettings</code>", styles['TableCellBold']), Paragraph("Эмулятор сети: искусственная односторонняя задержка (100–175 мс) и детерминированная потеря пакетов (5%).", styles['TableCell'])],
    ]
    t_roles = Table(roles_data, colWidths=[38*mm, 144*mm])
    t_roles.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,0), PRIMARY),
        ('BOX', (0,0), (-1,-1), 0.5, BORDER_COL),
        ('INNERGRID', (0,0), (-1,-1), 0.5, BORDER_COL),
        ('ROWBACKGROUNDS', (0,1), (-1,-1), [colors.white, LIGHT_BG]),
        ('TOPPADDING', (0,0), (-1,-1), 2.5),
        ('BOTTOMPADDING', (0,0), (-1,-1), 2.5),
        ('LEFTPADDING', (0,0), (-1,-1), 3),
        ('RIGHTPADDING', (0,0), (-1,-1), 3),
    ]))
    story.append(t_roles)
    
    # 2. КАК ЭТО РАБОТАЕТ
    story.append(Spacer(1, 4))
    story.append(Paragraph("2. Как это работает (Ключевые принципы)", styles['H1']))
    story.append(Paragraph(
        "• <b>Server Authority (Авторитет сервера):</b> Клиент <u>не имеет права</u> отправлять серверу свои готовые координаты <code>(X, Y, Z)</code>. "
        "Клиент отправляет только свой ввод через RPC (номер шага <code>sequence</code>, вектор <code>move</code>, флаг <code>dashRequested</code>). "
        "Сервер сам на своей стороне проверяет правила, кулдауны и рассчитывает физику.<br/>"
        "• <b>Client-Side Prediction (CSP):</b> Чтобы при задержке в 100 мс игрок не ждал 200 мс ответа, клиент мгновенно двигает персонажа локально "
        "(отклик <b>0.0 мс</b>) и сохраняет этот шаг в буфер <code>_pendingInputs</code>.<br/>"
        "• <b>Server Reconciliation:</b> Сервер присылает авторитетную позицию для подтверждённого шага. Если клиент ошибся в прогнозе "
        "(врезался в стену или считерил), клиент возвращает персонажа на координаты сервера и заново применяет поверх оставшиеся неподтверждённые ходы из <code>_pending</code>.",
        styles['Body']
    ))
    
    # 3. КАК ЗАПУСКАТЬ НА MAC
    story.append(Spacer(1, 4))
    story.append(Paragraph("3. Как запускать проект на Mac (2 независимых окна)", styles['H1']))
    story.append(Paragraph(
        "Чтобы на Mac открылись два отдельных окна без конфликтов оконного менеджера, выполните в двух окнах Терминала команду:",
        styles['Body']
    ))
    
    cmd_text = "/Users/tema/Documents/Разработка\\ многопользовательских\\ приложений\\ виртуальной\\ реальности/prac_1/unity/PredictionDemo/Builds/PR4PredictionDemo.app/Contents/MacOS/PredictionDemo"
    run_table_data = [
        [Paragraph("Окно 1 (Хост / Сервер):", styles['TableCellBold']), Paragraph(f"<code>{cmd_text}</code><br/><i>(в окне нажмите кнопку <b>«Start Host»</b>)</i>", styles['CmdBlock'])],
        [Paragraph("Окно 2 (Клиент):", styles['TableCellBold']), Paragraph(f"<code>{cmd_text}</code><br/><i>(в окне нажмите кнопку <b>«Start Client»</b>)</i>", styles['CmdBlock'])],
    ]
    t_run = Table(run_table_data, colWidths=[42*mm, 140*mm])
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
    story.append(Spacer(1, 4))
    
    # Танковый полигон обзор
    img_tank = str(media_dir / "tank_prediction_demo.png")
    tank_box = [
        [
            Image(img_tank, width=96*mm, height=54*mm),
            [
                Paragraph("<b>Общий вид: Танковый полигон и техника</b>", styles['QTitle']),
                Paragraph("<b>Локальный танк (зелёный):</b> корпус, гусеницы, поворотная башня и пушка с дульным тормозом. Управляется стрелками / WASD с авто-поворотом корпуса по курсу движения.", styles['ImgDesc']),
                Paragraph("<b>Удалённый танк (песочный):</b> позиция и поворот синхронизируются через интерполяцию с авторитетного сервера.", styles['ImgDesc']),
                Paragraph("<b>Арена полигона:</b> песчано-грунтовая поверхность, бетонный центральный бункер, капониры и ящики со снарядами.", styles['ImgDesc']),
            ]
        ]
    ]
    t_tank = Table(tank_box, colWidths=[98*mm, 84*mm])
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
    
    # PAGE BREAK ДЛЯ СКРИНШОТОВ И СЦЕНАРИЕВ
    story.append(PageBreak())
    
    # 4. ФОТОГРАФИИ И СЦЕНАРИЙ ДЕМОНСТРАЦИИ
    story.append(Paragraph("4. Иллюстрации и сценарий демонстрации с замерами HUD", styles['H1']))
    story.append(Paragraph(
        "Ниже приведены реальные снимки экранов из нашего прототипа с пояснениями, что на них зафиксировано и что говорить преподавателю:",
        styles['Body']
    ))
    
    # Скриншот 1: Baseline No Prediction
    img_w, img_h = 100*mm, 56.25*mm # 16:9 ratio
    
    card_t2 = [
        [
            Image(img_baseline, width=img_w, height=img_h),
            [
                Paragraph("<b>Тест T2: Задержка без предсказания</b>", styles['QTitle']),
                Paragraph("<b>Сетевые условия:</b> 100 ms one-way (200 ms RTT).<br/><b>Режим:</b> <code>Prediction: OFF</code>.", styles['ImgDesc']),
                Paragraph("<b>Что на экране в HUD:</b><br/>Строка <i>«Last dash reaction»</i> фиксирует задержку <b>210.4 мс</b>!", styles['ImgDesc']),
                Paragraph("<b>Пояснение преподавателю:</b><br/>Игрок нажимает рывок (Space), но персонаж стоит на месте 210 мс, пока запрос летит на сервер и возвращается подтверждение. Управление ощущается неотзывчивым.", styles['ImgDesc']),
            ]
        ]
    ]
    t_t2 = Table(card_t2, colWidths=[102*mm, 80*mm])
    t_t2.setStyle(TableStyle([
        ('BOX', (0,0), (-1,-1), 0.5, BORDER_COL),
        ('BACKGROUND', (0,0), (-1,-1), LIGHT_BG),
        ('VALIGN', (0,0), (-1,-1), 'TOP'),
        ('TOPPADDING', (0,0), (-1,-1), 4),
        ('BOTTOMPADDING', (0,0), (-1,-1), 4),
        ('LEFTPADDING', (0,0), (-1,-1), 4),
        ('RIGHTPADDING', (0,0), (-1,-1), 4),
    ]))
    story.append(t_t2)
    story.append(Spacer(1, 4))
    
    # Скриншот 2: Prediction Enabled
    card_t3 = [
        [
            Image(img_prediction, width=img_w, height=img_h),
            [
                Paragraph("<b>Тест T3: Мгновенный отклик с предсказанием</b>", styles['QTitle']),
                Paragraph("<b>Сетевые условия:</b> 100 ms one-way (200 ms RTT).<br/><b>Режим:</b> <code>Prediction: ON</code>.", styles['ImgDesc']),
                Paragraph("<b>Что на экране в HUD:</b><br/>Строка <i>«Last dash reaction»</i> показывает ровно <b>0.0 мс</b>!", styles['ImgDesc']),
                Paragraph("<b>Пояснение преподавателю:</b><br/>С включённым CSP персонаж совершает рывок немедленно. Игрок не чувствует задержки, а сервер параллельно валидирует действие и присылает ACK постфактум.", styles['ImgDesc']),
            ]
        ]
    ]
    t_t3 = Table(card_t3, colWidths=[102*mm, 80*mm])
    t_t3.setStyle(TableStyle([
        ('BOX', (0,0), (-1,-1), 0.5, BORDER_COL),
        ('BACKGROUND', (0,0), (-1,-1), LIGHT_BG),
        ('VALIGN', (0,0), (-1,-1), 'TOP'),
        ('TOPPADDING', (0,0), (-1,-1), 4),
        ('BOTTOMPADDING', (0,0), (-1,-1), 4),
        ('LEFTPADDING', (0,0), (-1,-1), 4),
        ('RIGHTPADDING', (0,0), (-1,-1), 4),
    ]))
    story.append(t_t3)
    story.append(Spacer(1, 4))
    
    # Скриншот 3: Correction Example
    card_t5 = [
        [
            Image(img_correction, width=img_w, height=img_h),
            [
                Paragraph("<b>Тест T5: Серверная коррекция расхождения</b>", styles['QTitle']),
                Paragraph("<b>Сетевые условия:</b> 175 ms задержки + 5% потерь.<br/><b>Действие:</b> читерский рывок на клавишу <code>B</code>.", styles['ImgDesc']),
                Paragraph("<b>Что на экране в HUD:</b><br/>Зафиксирована коррекция: <i>«Max error: <b>0.535 m</b>»</i>, <i>«corrections: 1»</i>.", styles['ImgDesc']),
                Paragraph("<b>Пояснение преподавателю:</b><br/>Клиент попытался сделать нелегальный двойной рывок. Сервер отклонил недопустимую дистанцию. Клиент мягко вернулся на позицию сервера без вылета игры.", styles['ImgDesc']),
            ]
        ]
    ]
    t_t5 = Table(card_t5, colWidths=[102*mm, 80*mm])
    t_t5.setStyle(TableStyle([
        ('BOX', (0,0), (-1,-1), 0.5, BORDER_COL),
        ('BACKGROUND', (0,0), (-1,-1), LIGHT_BG),
        ('VALIGN', (0,0), (-1,-1), 'TOP'),
        ('TOPPADDING', (0,0), (-1,-1), 4),
        ('BOTTOMPADDING', (0,0), (-1,-1), 4),
        ('LEFTPADDING', (0,0), (-1,-1), 4),
        ('RIGHTPADDING', (0,0), (-1,-1), 4),
    ]))
    story.append(t_t5)
    
    # PAGE BREAK ДЛЯ ШПАРГАЛКИ ОТВЕТОВ
    story.append(PageBreak())
    story.append(Paragraph("5. Ответы на контрольные вопросы преподавателя", styles['H1']))
    
    qa = [
        (
            "1. Почему сервер не должен безусловно доверять клиентской позиции (ActorLocation / Transform)?",
            "Если сервер принимает координаты напрямую от клиента, любой клиент может модифицировать значения в памяти процесса и телепортироваться сквозь стены или летать (speedhack, no-clip). Чтобы исключить читы, сервер обязан принимать только физический ввод (намерение) и самостоятельно рассчитывать перемещение по законам физики игрового мира.",
            "Иначе клиент может отправить любые сфабрикованные координаты и телепортироваться сквозь стены."
        ),
        (
            "2. В чём разница между Client-Side Prediction и Server Reconciliation?",
            "Prediction (предсказание) — это локальный шаг вперёд: клиент сразу применяет свой ввод, не дожидаясь ответа сервера, устраняя задержку управления. Reconciliation (сверка/согласование) — это механизм коррекции: клиент получает авторитетную позицию от сервера, и если локальный прогноз ошибся (коллизия со стеной или чит), клиент откатывается назад к серверной точке и заново проигрывает оставшиеся ходы.",
            "Prediction убирает задержку отклика, а Reconciliation исправляет локальные ошибки предсказания по эталону сервера."
        ),
        (
            "3. Что такое Saved Move (сохранённый ход) и зачем он хранится в очереди pending?",
            "Saved Move — это запись о совершённом локальном шаге (номер sequence, delta time, направление движения, запрос рывка), отправленная серверу, но ещё не подтверждённая им. Эта очередь необходима для повторного наката (re-play): когда сервер присылает поправку из прошлого, клиент не сбрасывает игрока назад, а накатывает все ещё не подтверждённые ходы поверх поправки.",
            "Чтобы после серверного отката быстро проиграть ещё не подтверждённые ходы игрока и сохранить актуальное положение."
        ),
        (
            "4. Что такое Network Smoothing и чем оно отличается от Reconciliation?",
            "Reconciliation — это логическое исправление физических координат персонажа (жёсткая серверная истина). Network Smoothing (сетевое сглаживание) — это чисто визуальная интерполяция меша персонажа, которая сглаживает рывки при перемещении удалённых игроков и маскирует скачок позиции при серверной коррекции, не нарушая физику.",
            "Reconciliation меняет физическую позицию, а Smoothing плавно анимирует видимую модельку для глаз игрока."
        ),
    ]
    
    for q, a_full, a_short in qa:
        q_box = [
            Paragraph(f"<b>{q}</b>", styles['QTitle']),
            Spacer(1, 1),
            Paragraph(a_full, styles['QAnswer']),
            Spacer(1, 1),
            Paragraph(f"<b>Ответ у доски:</b> {a_short}", styles['QShort'])
        ]
        t_q = Table([[q_box]], colWidths=[182*mm])
        t_q.setStyle(TableStyle([
            ('BACKGROUND', (0,0), (-1,-1), CARD_BG),
            ('BOX', (0,0), (-1,-1), 0.5, SECONDARY),
            ('TOPPADDING', (0,0), (-1,-1), 3),
            ('BOTTOMPADDING', (0,0), (-1,-1), 3),
            ('LEFTPADDING', (0,0), (-1,-1), 5),
            ('RIGHTPADDING', (0,0), (-1,-1), 5),
        ]))
        story.append(t_q)
        story.append(Spacer(1, 2.5))
        
    doc.build(story, canvasmaker=NumberedCanvas)
    print(f"Illustrated PDF successfully created at: {target_path}")

if __name__ == '__main__':
    root = Path(__file__).resolve().parent.parent
    target = root / "МЕТОДИЧКА_ПР4_С_ФОТО.pdf"
    build_pdf(target)
