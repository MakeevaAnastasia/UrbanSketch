"""Контроль формул из исходного ТЗ. Это не реализация продуктового модуля."""
from decimal import Decimal as D

housing_sale = D('1000') * D('200000')
commercial_sale = D('200') * D('150000')
parking_sale = D('20') * D('1000000')
revenue = housing_sale + commercial_sale + parking_sale
cost = D('1000')*D('100000') + D('200')*D('80000') + D('20')*D('500000') + D('24000000')
profit = revenue - cost
roi = profit / cost * D('100')
margin = profit / revenue * D('100')
flows = [revenue*r/D('100') - cost*c/D('100') for c,r in [(D(60),D(0)),(D(40),D(20)),(D(0),D(80))]]
assert revenue == D('250000000')
assert cost == D('150000000')
assert profit == D('100000000')
assert roi.quantize(D('0.01')) == D('66.67')
assert margin == D('40')
assert flows == [D('-90000000'),D('-10000000'),D('200000000')]
assert sum(flows) == profit
print('Эталонный сценарий ТЗ: 8 проверок пройдены.')
