import { Search, Download, Plus, Edit2, Trash2, AlertTriangle } from 'lucide-react';
import type { Category, Product } from '../App';

type CategoryDetailProps = {
  category: Category;
  onBack: () => void;
  onNewProduct: () => void;
  onEditProduct: (product: Product) => void;
};

const products: Product[] = [
  { code: 'TOR-001', name: 'Tornillo M3x10', stock: 150, minimum: 50, unit: 'pza', price: 0.50, location: 'A-1' },
  { code: 'TOR-002', name: 'Tornillo M4x15', stock: 20, minimum: 30, unit: 'pza', price: 0.75, location: 'A-1' },
  { code: 'TOR-003', name: 'Tornillo M5x20', stock: 200, minimum: 100, unit: 'pza', price: 1.00, location: 'A-2' },
  { code: 'TOR-004', name: 'Tuerca M3', stock: 45, minimum: 50, unit: 'pza', price: 0.30, location: 'A-1' },
  { code: 'TOR-005', name: 'Arandela M3', stock: 500, minimum: 100, unit: 'pza', price: 0.10, location: 'A-3' },
  { code: 'TOR-006', name: 'Tornillo Allen M6', stock: 80, minimum: 20, unit: 'pza', price: 1.50, location: 'A-2' },
  { code: 'TOR-007', name: 'Perno M8x30', stock: 10, minimum: 25, unit: 'pza', price: 2.00, location: 'B-1' },
  { code: 'TOR-008', name: 'Rondana plana M4', stock: 300, minimum: 50, unit: 'pza', price: 0.15, location: 'A-1' },
];

export default function CategoryDetail({ category, onBack, onNewProduct, onEditProduct }: CategoryDetailProps) {
  const lowStockProducts = products.filter(p => p.stock < p.minimum);
  const totalValue = products.reduce((sum, p) => sum + (p.stock * p.price), 0);

  return (
    <div className="w-full h-full flex flex-col overflow-hidden" style={{ backgroundColor: '#FFFFFF', borderRadius: '0' }}>
      {/* Header */}
      <div className="flex-none" style={{ borderBottom: '1px solid #E2E8F0', padding: '20px 24px' }}>
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-4">
            <button
              onClick={onBack}
              className="hover:opacity-70 transition-opacity"
              style={{ 
                fontSize: '14px',
                color: '#1D4ED8',
                fontFamily: 'Segoe UI, sans-serif',
                background: 'none',
                border: 'none',
                cursor: 'pointer',
                padding: '0'
              }}
            >
              ← Volver
            </button>

            <div 
              style={{ 
                width: '4px', 
                height: '48px', 
                backgroundColor: category.color,
                borderRadius: '2px'
              }}
            />
            
            <div>
              <div style={{ fontSize: '24px', fontWeight: '700', color: '#0F172A', lineHeight: '1.2', fontFamily: 'Segoe UI, sans-serif' }}>
                {category.name}
              </div>
              <div style={{ fontSize: '14px', color: '#64748B', marginTop: '2px', fontFamily: 'Segoe UI, sans-serif' }}>
                {category.productsCount} productos
              </div>
            </div>
          </div>

          <div className="flex items-center gap-3">
            <button
              className="flex items-center gap-2 px-4 py-2 bg-white hover:bg-gray-50 transition-colors"
              style={{ 
                border: '1px solid #E2E8F0', 
                borderRadius: '8px',
                fontSize: '13px',
                color: '#0F172A',
                fontFamily: 'Segoe UI, sans-serif'
              }}
            >
              <Download size={16} />
              Exportar
            </button>

            <button
              onClick={onNewProduct}
              className="flex items-center gap-2 px-4 py-2 hover:opacity-90 transition-opacity"
              style={{ 
                backgroundColor: '#1D4ED8', 
                color: 'white',
                borderRadius: '8px',
                fontSize: '13px',
                fontWeight: '700',
                fontFamily: 'Segoe UI, sans-serif'
              }}
            >
              <Plus size={18} strokeWidth={2.5} />
              Nuevo Producto
            </button>
          </div>
        </div>
      </div>

      {/* Filter Bar */}
      <div 
        className="flex-none flex items-center justify-between gap-4" 
        style={{ 
          backgroundColor: '#F8FAFC', 
          padding: '16px 24px',
          borderBottom: '1px solid #F1F5F9'
        }}
      >
        <div className="relative" style={{ width: '300px' }}>
          <Search 
            className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400" 
            size={18} 
          />
          <input
            type="text"
            placeholder="Buscar por código o nombre..."
            className="w-full pl-10 pr-4 py-2 bg-white"
            style={{
              border: '1px solid #E2E8F0',
              borderRadius: '8px',
              fontSize: '13px',
              color: '#0F172A',
              fontFamily: 'Segoe UI, sans-serif',
              outline: 'none'
            }}
          />
        </div>

        <div className="flex items-center gap-3">
          <label 
            className="flex items-center gap-2 px-3 py-2 cursor-pointer hover:bg-amber-50 transition-colors"
            style={{ 
              border: '1px solid #E2E8F0',
              borderRadius: '8px',
              fontSize: '13px',
              color: '#0F172A',
              fontFamily: 'Segoe UI, sans-serif'
            }}
          >
            <input type="checkbox" className="w-4 h-4" />
            <AlertTriangle size={14} className="text-amber-600" />
            Solo stock bajo
          </label>

          <select
            className="px-3 py-2"
            style={{
              border: '1px solid #E2E8F0',
              borderRadius: '8px',
              fontSize: '13px',
              color: '#0F172A',
              fontFamily: 'Segoe UI, sans-serif',
              outline: 'none',
              backgroundColor: 'white'
            }}
          >
            <option>Todas las ubicaciones</option>
            <option>A-1</option>
            <option>A-2</option>
            <option>A-3</option>
            <option>B-1</option>
          </select>
        </div>

        <div style={{ fontSize: '12px', color: '#94A3B8', fontFamily: 'Segoe UI, sans-serif' }}>
          Mostrando {products.length} de {products.length}
        </div>
      </div>

      {/* Table */}
      <div className="flex-1 overflow-auto" style={{ backgroundColor: '#F8FAFC' }}>
        <table className="w-full" style={{ borderCollapse: 'separate', borderSpacing: '0' }}>
          <thead style={{ position: 'sticky', top: 0, zIndex: 10 }}>
            <tr style={{ backgroundColor: '#F8FAFC' }}>
              <th style={{ 
                padding: '12px 24px', 
                textAlign: 'left', 
                fontSize: '12px', 
                fontWeight: '600',
                color: '#64748B',
                textTransform: 'uppercase',
                fontFamily: 'Segoe UI, sans-serif',
                borderBottom: '1px solid #E2E8F0'
              }}>
                Código
              </th>
              <th style={{ 
                padding: '12px 24px', 
                textAlign: 'left', 
                fontSize: '12px', 
                fontWeight: '600',
                color: '#64748B',
                textTransform: 'uppercase',
                fontFamily: 'Segoe UI, sans-serif',
                borderBottom: '1px solid #E2E8F0'
              }}>
                Nombre
              </th>
              <th style={{ 
                padding: '12px 24px', 
                textAlign: 'left', 
                fontSize: '12px', 
                fontWeight: '600',
                color: '#64748B',
                textTransform: 'uppercase',
                fontFamily: 'Segoe UI, sans-serif',
                borderBottom: '1px solid #E2E8F0'
              }}>
                Stock
              </th>
              <th style={{ 
                padding: '12px 24px', 
                textAlign: 'left', 
                fontSize: '12px', 
                fontWeight: '600',
                color: '#64748B',
                textTransform: 'uppercase',
                fontFamily: 'Segoe UI, sans-serif',
                borderBottom: '1px solid #E2E8F0'
              }}>
                Mínimo
              </th>
              <th style={{ 
                padding: '12px 24px', 
                textAlign: 'left', 
                fontSize: '12px', 
                fontWeight: '600',
                color: '#64748B',
                textTransform: 'uppercase',
                fontFamily: 'Segoe UI, sans-serif',
                borderBottom: '1px solid #E2E8F0'
              }}>
                Unidad
              </th>
              <th style={{ 
                padding: '12px 24px', 
                textAlign: 'left', 
                fontSize: '12px', 
                fontWeight: '600',
                color: '#64748B',
                textTransform: 'uppercase',
                fontFamily: 'Segoe UI, sans-serif',
                borderBottom: '1px solid #E2E8F0'
              }}>
                Precio Unit.
              </th>
              <th style={{ 
                padding: '12px 24px', 
                textAlign: 'left', 
                fontSize: '12px', 
                fontWeight: '600',
                color: '#64748B',
                textTransform: 'uppercase',
                fontFamily: 'Segoe UI, sans-serif',
                borderBottom: '1px solid #E2E8F0'
              }}>
                Ubicación
              </th>
              <th style={{ 
                padding: '12px 24px', 
                textAlign: 'center', 
                fontSize: '12px', 
                fontWeight: '600',
                color: '#64748B',
                textTransform: 'uppercase',
                fontFamily: 'Segoe UI, sans-serif',
                borderBottom: '1px solid #E2E8F0'
              }}>
                Acciones
              </th>
            </tr>
          </thead>
          <tbody>
            {products.map((product) => {
              const isLowStock = product.stock < product.minimum;
              const isHighStock = product.stock > product.minimum * 2;
              
              return (
                <tr 
                  key={product.code}
                  className="hover:bg-gray-50 transition-colors"
                  style={{ 
                    backgroundColor: isLowStock ? '#FFFBEB' : 'white',
                    borderLeft: isLowStock ? '3px solid #F59E0B' : '3px solid transparent',
                  }}
                >
                  <td style={{ 
                    padding: '12px 24px', 
                    fontSize: '13px', 
                    color: '#0F172A',
                    fontFamily: 'Segoe UI, sans-serif',
                    borderBottom: '1px solid #F1F5F9'
                  }}>
                    {product.code}
                  </td>
                  <td style={{ 
                    padding: '12px 24px', 
                    fontSize: '13px', 
                    color: '#0F172A',
                    fontFamily: 'Segoe UI, sans-serif',
                    borderBottom: '1px solid #F1F5F9'
                  }}>
                    {product.name}
                  </td>
                  <td style={{ 
                    padding: '12px 24px', 
                    fontSize: '13px', 
                    color: isLowStock ? '#D97706' : isHighStock ? '#48BB78' : '#0F172A',
                    fontWeight: isLowStock ? '700' : '400',
                    fontFamily: 'Segoe UI, sans-serif',
                    borderBottom: '1px solid #F1F5F9'
                  }}>
                    <div className="flex items-center gap-2">
                      {isLowStock && <AlertTriangle size={14} className="text-amber-600" />}
                      {product.stock}
                    </div>
                  </td>
                  <td style={{ 
                    padding: '12px 24px', 
                    fontSize: '13px', 
                    color: '#0F172A',
                    fontFamily: 'Segoe UI, sans-serif',
                    borderBottom: '1px solid #F1F5F9'
                  }}>
                    {product.minimum}
                  </td>
                  <td style={{ 
                    padding: '12px 24px', 
                    fontSize: '13px', 
                    color: '#0F172A',
                    fontFamily: 'Segoe UI, sans-serif',
                    borderBottom: '1px solid #F1F5F9'
                  }}>
                    {product.unit}
                  </td>
                  <td style={{ 
                    padding: '12px 24px', 
                    fontSize: '13px', 
                    color: '#0F172A',
                    fontFamily: 'Segoe UI, sans-serif',
                    borderBottom: '1px solid #F1F5F9'
                  }}>
                    ${product.price.toFixed(2)}
                  </td>
                  <td style={{ 
                    padding: '12px 24px', 
                    fontSize: '13px', 
                    color: '#0F172A',
                    fontFamily: 'Segoe UI, sans-serif',
                    borderBottom: '1px solid #F1F5F9'
                  }}>
                    {product.location}
                  </td>
                  <td style={{ 
                    padding: '12px 24px', 
                    textAlign: 'center',
                    borderBottom: '1px solid #F1F5F9'
                  }}>
                    <div className="flex items-center justify-center gap-2">
                      <button
                        onClick={() => onEditProduct(product)}
                        className="p-1 hover:bg-blue-100 rounded transition-colors"
                        style={{ color: '#64748B' }}
                        title="Editar"
                      >
                        <Edit2 size={16} className="hover:text-blue-600" />
                      </button>
                      <button
                        className="p-1 hover:bg-red-100 rounded transition-colors"
                        style={{ color: '#64748B' }}
                        title="Eliminar"
                      >
                        <Trash2 size={16} className="hover:text-red-600" />
                      </button>
                    </div>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      {/* Footer */}
      <div 
        className="flex-none flex items-center justify-between" 
        style={{ 
          borderTop: '1px solid #E2E8F0', 
          padding: '12px 24px',
          backgroundColor: '#FFFFFF'
        }}
      >
        <div className="flex items-center gap-2" style={{ fontSize: '13px', color: '#D97706', fontFamily: 'Segoe UI, sans-serif' }}>
          <AlertTriangle size={14} />
          {lowStockProducts.length} productos con stock bajo
        </div>
        <div style={{ fontSize: '14px', fontWeight: '600', color: '#0F172A', fontFamily: 'Segoe UI, sans-serif' }}>
          Valor total de inventario: ${totalValue.toFixed(2)}
        </div>
      </div>
    </div>
  );
}
